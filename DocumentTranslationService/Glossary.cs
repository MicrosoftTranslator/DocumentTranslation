using Azure.AI.Translation.Document;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace DocumentTranslationService.Core
{
    /// <summary>
    /// Holds the glossary and the functions to maintain it. 
    /// </summary>
    public class Glossary
    {
        /// <summary>
        /// Dictionary of Glossary Filename and various glossary information
        /// The string is the file name.
        /// </summary>
        public Dictionary<string, TranslationGlossary> Glossaries { get; private set; } = new();
        /// <summary>
        /// Dictionary of plain Uri glossaries
        /// For use with Managed Identity
        /// </summary>
        public Dictionary<string, TranslationGlossary> PlainUriGlossaries { get; private set; }

        /// <summary>
        /// Holds the Container for the glossary files
        /// </summary>
        private BlobContainerClient containerClient;
        private readonly DocumentTranslationService translationService;

        /// <summary>
        /// Fires when a file submitted as glossary was not used.
        /// </summary>
        public event EventHandler<List<string>> OnGlossaryDiscarded;

        /// <summary>
        /// Fires when the upload complete.
        /// Returns the number of files uploaded, and the combined size.
        /// </summary>
        public event EventHandler<(int, long)> OnUploadComplete;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="translationService"></param>
        /// <param name="glossaryFiles"></param>
        public Glossary(DocumentTranslationService translationService, List<string> glossaryFiles)
        {
            if ((glossaryFiles is null) || (glossaryFiles.Count == 0))
            {
                Glossaries = null;
                return;
            }
            foreach (string file in glossaryFiles)
            {
                Glossaries.TryAdd(file, null);
            }
            this.translationService = translationService;
        }

        /// <summary>
        /// Upload the glossary files named in the GlossaryFiles property.
        /// </summary>
        /// <param name="storageConnectionString">The storage connection string to use for container creation</param>
        /// <param name="containerNameBase">The GUID-infused base name to use as the container name</param>
        /// <returns>Task</returns>
        /// <remarks>Serious optimization possible here. The container should be permanent, and upload only changed files, or no files at all, and still use them.</remarks>
        public async Task<(int, long)> UploadAsync(string storageConnectionString, string containerNameBase)
        {
            //Expand directory
            if (Glossaries is null) return (0, 0);
            List<string> discards = new();
            foreach (var glossary in Glossaries)
            {
                if (!File.Exists(glossary.Key))
                {
                    Debug.WriteLine($"Glossary file ignored: {glossary.Key}");
                    discards.Add(glossary.Key);
                    OnGlossaryDiscarded?.Invoke(this, discards);
                    Glossaries = null;
                    return (0, 0);
                }
                if (File.GetAttributes(glossary.Key) == FileAttributes.Directory)
                {
                    Glossaries.Remove(glossary.Key);
                    foreach (var file in Directory.EnumerateFiles(glossary.Key))
                    {
                        Glossaries.Add(file, null);
                    }
                }
            }
            //Remove files that don't match the allowed extensions
            foreach (var glossary in Glossaries)
            {
                if (!(translationService.GlossaryExtensions.Contains(Path.GetExtension(glossary.Key))))
                {
                    Glossaries.Remove(glossary.Key);
                    discards.Add(glossary.Key);
                }
            }
            if (discards is not null)
                foreach (string fileName in discards)
                {
                    Debug.WriteLine($"Glossary files ignored: {fileName}");
                    OnGlossaryDiscarded?.Invoke(this, discards);
                }
            //Exit if no files are left
            if (Glossaries.Count == 0)
            {
                Glossaries = null;
                return (0, 0);
            }

            //Create glossary container
            Debug.WriteLine("START - glossary container creation.");
            BlobContainerClient glossaryContainer = new(storageConnectionString, containerNameBase + "gls");
            await glossaryContainer.CreateIfNotExistsAsync();
            this.containerClient = glossaryContainer;

            //Do the upload
            Debug.WriteLine("START - glossary upload.");
            System.Threading.SemaphoreSlim semaphore = new(10); //limit the number of concurrent uploads
            PlainUriGlossaries = new(Glossaries);
            int fileCounter = 0;
            long uploadSize = 0;
            List<Task> uploads = new();
            foreach (var glossary in Glossaries)
            {
                await semaphore.WaitAsync();
                string filename = glossary.Key;
                //Use a GUID instead of the file name in the container, because the glossary files in separate local folders might have the same file name. 
                BlobClient blobClient = new(translationService.StorageConnectionString, glossaryContainer.Name, Guid.NewGuid().ToString() + Path.GetExtension(filename));
                uploads.Add(blobClient.UploadAsync(filename, true));
                Uri sasUriGlossaryBlob = blobClient.GenerateSasUri(BlobSasPermissions.All, DateTimeOffset.UtcNow + TimeSpan.FromHours(5));
                Debug.WriteLine($"Glossary URI: {sasUriGlossaryBlob.AbsoluteUri}");
                TranslationGlossary translationGlossary = new(sasUriGlossaryBlob, Path.GetExtension(glossary.Key)[1..].ToUpperInvariant());
                Glossaries[glossary.Key] = translationGlossary;
                TranslationGlossary plainUriTranslationGlossary = new(blobClient.Uri, Path.GetExtension(glossary.Key)[1..].ToUpperInvariant());
                PlainUriGlossaries[glossary.Key] = plainUriTranslationGlossary;
                fileCounter++;
                uploadSize += new FileInfo(filename).Length;
                semaphore.Release();
                Debug.WriteLine(String.Format($"Glossary file {filename} uploaded."));
            }
            await Task.WhenAll(uploads);
            Debug.WriteLine($"Glossary: {fileCounter} files, {uploadSize} bytes uploaded.");
            OnUploadComplete?.Invoke(this, (fileCounter, uploadSize));
            return (fileCounter, uploadSize);
        }

        public async Task<Azure.Response> DeleteAsync()
        {
            if (Glossaries is not null)
            {
                try
                {
                    Azure.Response response = await containerClient.DeleteAsync();
                    return response;
                }
                catch (Azure.RequestFailedException ex)
                {
                    Debug.WriteLine($"Glossary deletion failed: {containerClient.Name}: {ex.Message}");
                }
            }
            return null;
        }
    }
}
