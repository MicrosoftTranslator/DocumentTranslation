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
        public List<string> GlossaryFiles { get => glossaryFiles; set => glossaryFiles = value; }

        public BlobContainerClient ContainerClient { get { return containerClient; } }

        private BlobContainerClient containerClient;
        private List<string> glossaryFiles;
        private bool usingGlossary;
        private readonly DocumentTranslationService translationService;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="translationService"></param>
        /// <param name="glossaryFiles"></param>
        public Glossary(DocumentTranslationService translationService, List<string> glossaryFiles = null)
        {
            if ((glossaryFiles is null) || (glossaryFiles.Count == 0))
            {
                usingGlossary = false;
                return;
            }
            else usingGlossary = true;
            GlossaryFiles = glossaryFiles;
            this.translationService = translationService;
        }

        /// <summary>
        /// Creates the glossary container on Azure storage
        /// </summary>
        /// <param name="storageConnectionString">Storage connection string directly from the Azure portal</param>
        /// <param name="containerNameBase">The naming pattern for the container</param>
        /// <returns></returns>
        public async Task CreateContainerAsync(string storageConnectionString, string containerNameBase)
        {
            if (!usingGlossary) return;
            BlobContainerClient glossaryContainer = new(storageConnectionString, containerNameBase + "gls");
            var GlossaryContainerTask = glossaryContainer.CreateIfNotExistsAsync();
            this.containerClient = glossaryContainer;
            await GlossaryContainerTask;
        }

        /// <summary>
        /// Upload the glossary files named in the GlossaryFiles property.
        /// </summary>
        /// <returns>Task</returns>
        /// <remarks>Serious optimization possible here. The container should be permanent, and upload only changed files, or no files at all, and still use them.</remarks>
        public async Task<(int, long)> UploadAsync()
        {
            if (!usingGlossary) return (0, 0);
            List<string> selecteds = new();
            foreach (string filename in GlossaryFiles)
            {
                if (File.GetAttributes(filename) == FileAttributes.Directory)
                    foreach (var file in Directory.EnumerateFiles(filename))
                    {
                        selecteds.Add(file);
                    }
                else selecteds.Add(filename);
            }

            List<string> discards;
            (GlossaryFiles, discards) = DocumentTranslationBusiness.FilterByExtension(selecteds, translationService.GlossaryExtensions);
            if (discards is not null)
            {
                foreach (string fileName in discards)
                {
                    Debug.WriteLine($"Glossary files ignored: {fileName}");
                }
            }
            if (selecteds.Count == 0)
            {
                usingGlossary = false;
                return (0, 0);
            }
            System.Threading.SemaphoreSlim semaphore = new(10); //limit the number of concurrent uploads
            int fileCounter = 0;
            long uploadSize = 0;
            if (GlossaryFiles is not null)
            {
                List<Task> uploads = new();
                foreach (string filename in GlossaryFiles)
                {
                    await semaphore.WaitAsync();
                    using FileStream fileStream = File.OpenRead(filename);
                    BlobClient blobClient = new(translationService.StorageConnectionString, translationService.ContainerClientSource.Name, DocumentTranslationBusiness.Normalize(filename));
                    try
                    {
                        uploads.Add(blobClient.UploadAsync(fileStream, true));
                        fileCounter++;
                        uploadSize += new FileInfo(fileStream.Name).Length;
                    }
                    catch (System.AggregateException e)
                    {
                        Debug.WriteLine($"Uploading file {fileStream.Name} failed with {e.Message}");
                    }
                    semaphore.Release();
                    Debug.WriteLine(String.Format($"Glossary file {fileStream.Name} uploaded."));
                }
                await Task.WhenAll(uploads);
            }
            return (fileCounter, uploadSize);
        }

        public Uri GenerateSasUri()
        {
            if ((GlossaryFiles is null) || (GlossaryFiles.Count == 0)) return null;
            Uri sasUriGlossary = containerClient.GenerateSasUri(BlobContainerSasPermissions.All, DateTimeOffset.UtcNow + TimeSpan.FromHours(5));
            return sasUriGlossary;
        }

        public async Task DeleteAsync()
        {
            if ((GlossaryFiles is null) || (GlossaryFiles.Count == 0)) return;
            await containerClient.DeleteAsync();
            return;
        }
    }
}
