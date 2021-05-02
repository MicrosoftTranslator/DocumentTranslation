using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace DocumentTranslationService.Core
{
    public class Glossary
    {
        public List<string> GlossaryFiles { get; set; }

        public BlobContainerClient ContainerClient { get { return containerClient; } }

        private BlobContainerClient containerClient;

        private readonly DocumentTranslationService translationService;
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="translationService"></param>
        /// <param name="glossaryFiles"></param>
        public Glossary(DocumentTranslationService translationService, List<string> glossaryFiles = null)
        {
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
            if (GlossaryFiles is not null)
            {
                BlobContainerClient glossaryContainer = new(storageConnectionString, containerNameBase + "gls");
                var GlossaryContainerTask = glossaryContainer.CreateIfNotExistsAsync();
                this.containerClient = glossaryContainer;
                await GlossaryContainerTask;
            }
        }

        /// <summary>
        /// Upload the glossary files named in the GlossaryFiles property.
        /// </summary>
        /// <returns>Task</returns>
        /// <remarks>Serious optimization possible here. The container should be permanent, and upload only changed files, or no files at all, and still use them.</remarks>
        public async Task UploadAsync()
        {
            List<string> discards = new();
            (GlossaryFiles, discards) = DocumentTranslationBusiness.FilterByExtension(GlossaryFiles, translationService.GlossaryExtensions);
            if (discards is not null)
            {
                foreach (string fileName in discards)
                {
                    Debug.WriteLine($"Glossary files ignored: {fileName}");
                }
            }
            System.Threading.SemaphoreSlim semaphore = new(10); //limit the number of concurrent uploads
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
        }

        public Uri GenerateSasUri()
        {
            if (GlossaryFiles is not null)
            {
                Uri sasUriGlossary = containerClient.GenerateSasUri(BlobContainerSasPermissions.All, DateTimeOffset.UtcNow + TimeSpan.FromHours(1));
                return sasUriGlossary;
            }
            else return null;
        }

        public async Task DeleteAsync()
        {
            if (GlossaryFiles is not null)
            {
                await containerClient.DeleteAsync();
            }
        }
    }
}
