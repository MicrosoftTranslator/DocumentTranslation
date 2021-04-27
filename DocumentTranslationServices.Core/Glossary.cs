using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace DocumentTranslationServices.Core
{
    public class Glossary
    {
        public string GlossaryFile { get; set; }

        public BlobContainerClient ContainerClient { get { return containerClient; } }

        private BlobContainerClient containerClient;

        private readonly DocumentTranslationService translationService;
        
        public Glossary(DocumentTranslationService translationService, string glossaryFile = null)
        {
            GlossaryFile = glossaryFile;
            this.translationService = translationService;
        }

        public async Task CreateContainerAsync(string storageConnectionString, string containerNameBase)
        {
            if (!String.IsNullOrEmpty(GlossaryFile))
            {
                BlobContainerClient glossaryContainer = new(storageConnectionString, containerNameBase + "gls");
                var GlossaryContainerTask = glossaryContainer.CreateIfNotExistsAsync();
                this.containerClient = glossaryContainer;
                await GlossaryContainerTask;
            }
        }

        public async Task UploadAsync()
        {
            if (!String.IsNullOrEmpty(GlossaryFile))
            {
                using FileStream fileStream = File.OpenRead(this.GlossaryFile);
                BlobClient blobClient = new(translationService.StorageConnectionString, translationService.ContainerClientSource.Name, DocumentTranslationBusiness.Normalize(this.GlossaryFile));
                try
                {
                    await blobClient.UploadAsync(fileStream, true);
                }
                catch (System.AggregateException e)
                {
                    Debug.WriteLine($"Uploading file {fileStream.Name} failed with {e.Message}");
                }
                Debug.WriteLine(String.Format($"File {this.GlossaryFile} uploaded."));
            }
        }

        public Uri GenerateSasUri()
        {
            if (!String.IsNullOrEmpty(GlossaryFile))
            {
                Uri sasUriGlossary = containerClient.GenerateSasUri(BlobContainerSasPermissions.All, DateTimeOffset.UtcNow + TimeSpan.FromHours(1));
                return sasUriGlossary;
            }
            else return null;
        }

        public async Task DeleteAsync()
        {
            if (!String.IsNullOrEmpty(GlossaryFile))
            {
                await containerClient.DeleteAsync();
            }
        }
    }
}
