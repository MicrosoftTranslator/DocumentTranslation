using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using System.Net.Http;
using System.Text;
using System.Linq;
using Azure.Storage.Blobs.Models;

namespace DocumentTranslationServices.Core
{
    public class DocumentTranslation
    {
        #region Properties
        /// <summary>
        /// The "Connection String" of the Azure blob storage resource. Get from properties of Azure storage.
        /// </summary>
        private string StorageConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Your Azure Translator subscription key. Get from properties of the Translator resource
        /// </summary>
        private string SubscriptionKey { get; set; } = string.Empty;

        /// <summary>
        /// The name of the Azure Translator resource
        /// </summary>
        private string AzureResourceName { get; set; } = string.Empty;

        /// <summary>
        /// Obtain status of document translation 
        /// </summary>
        public event EventHandler<StatusResponse> DocumentTranslationStatus;

        private string processingLocation = string.Empty;

        private StatusResponse statusResponse;

        internal BlobContainerClient ContainerClient_source { get; set; }
        internal BlobContainerClient ContainerClient_target { get; set; }
        internal BlobContainerClient ContainerClient_glossary { get; set; }

        #endregion Properties
        #region Constants
        /// <summary>
        /// The base URL template for making translation requests.
        /// {0} is the name of the Translator resource.
        /// </summary>
        private const string baseUriTemplate = ".cognitiveservices.azure.com/translator/text/batch/v1.0-preview.1/batches"; 
        #endregion Constants
        #region Methods

        /// <summary>
        /// Constructor
        /// </summary>
        public DocumentTranslation(string SubscriptionKey, string AzureResourceName, string StorageConnectionString)
        {
            this.SubscriptionKey = SubscriptionKey;
            this.AzureResourceName = AzureResourceName;
            this.StorageConnectionString = StorageConnectionString;
            Debug.WriteLine("DocumentTranslation: Construction complete.");
        }
        public async Task Run(string[] filestotranslate, string tolanguage)
        {
            List<string> list = new();
            foreach(string file in filestotranslate)
            {
                list.Add(file);
            }
            await Run(list, tolanguage);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filestotranslate"></param>
        /// <returns></returns>
        public async Task Run(List<string> filestotranslate, string tolanguage)
        {
            //Create the containers
            string containerNameBase = "doctr" + Guid.NewGuid().ToString();

            BlobContainerClient sourceContainer = new(StorageConnectionString, containerNameBase + "src");
            var sourceContainerTask = sourceContainer.CreateIfNotExistsAsync();
            this.ContainerClient_source = sourceContainer;
            BlobContainerClient targetContainer = new(StorageConnectionString, containerNameBase + "tgt");
            _ = targetContainer.CreateIfNotExistsAsync();
            this.ContainerClient_target = targetContainer;
            BlobContainerClient glossaryContainer = new(StorageConnectionString, containerNameBase + "gls");
            _ = glossaryContainer.CreateIfNotExistsAsync();
            this.ContainerClient_glossary = glossaryContainer;

            await sourceContainerTask;
            Debug.WriteLine("Source container created");

            //Upload documents
            if((filestotranslate.Count ==1)
                && (File.GetAttributes(filestotranslate[0]) == FileAttributes.Directory))
            {
                foreach(var file in Directory.EnumerateFiles(filestotranslate[0]))
                {
                    filestotranslate.Add(file);
                }
                filestotranslate.RemoveAt(0);
            }

            List<Task> uploads = new();
            using System.Threading.SemaphoreSlim semaphore = new(100); 
            foreach (var filename in filestotranslate)
            {
                await semaphore.WaitAsync();
                using FileStream fileStream = File.OpenRead(filename);
                BlobClient blobClient = new(StorageConnectionString, ContainerClient_source.Name, Normalize(filename));
                uploads.Add(blobClient.UploadAsync(fileStream, true));
                semaphore.Release();
                Debug.WriteLine(String.Format("File {0} uploaded.", filename));
            }
            Debug.WriteLine("Awaiting upload task completion.");
            await Task.WhenAll(uploads);
            semaphore.Dispose();
            Debug.WriteLine("Upload complete. {0} files uploaded.", uploads.Count);

            //Translate the container content
            Uri sasUriSource = sourceContainer.GenerateSasUri(BlobContainerSasPermissions.All, DateTimeOffset.UtcNow + TimeSpan.FromHours(1));
            Uri sasUriTarget = targetContainer.GenerateSasUri(BlobContainerSasPermissions.All, DateTimeOffset.UtcNow + TimeSpan.FromHours(1));
            Uri sasUriGlossary = glossaryContainer.GenerateSasUri(BlobContainerSasPermissions.All, DateTimeOffset.UtcNow + TimeSpan.FromHours(1));
            DocumentTranslationSource documentTranslationSource = new() { SourceUrl = sasUriSource.ToString() };
            DocumentTranslationTarget documentTranslationTarget = new() { language = tolanguage, targetUrl = sasUriTarget.ToString() };
            List<DocumentTranslationTarget> documentTranslationTargets = new() { documentTranslationTarget };

            DocumentTranslationInput input = new() { storageType="folder", source = documentTranslationSource, targets = documentTranslationTargets };

            processingLocation = await SubmitTranslationRequest(input);
            Debug.WriteLine("Processing-Location: "+ processingLocation);
            if (processingLocation is null)
            {
                Debug.WriteLine("ERROR: Start of translation job failed.");
                return;
            }

            //check on status
            StatusResponse statusResult = await CheckStatus(processingLocation);
            if (string.IsNullOrEmpty(statusResult.id))
            {
                Debug.WriteLine("ERROR: Failed to get status. Aborting.");
                return;
            }

            do
            {
                await Task.Delay(1000);
                statusResult = await CheckStatus(processingLocation);
            }
            while ((statusResult.status != "Succeeded") && (!statusResult.status.Contains("Failed")));
            if (statusResult.status == "Succeeded")
            {
                string directoryName = Path.GetDirectoryName(filestotranslate[0]);
                DirectoryInfo directory =  Directory.CreateDirectory(directoryName+"."+tolanguage);
                await DownloadTheTranslations(directory.FullName);
            }
            await DeleteTheContainers();
            Debug.WriteLine("Run: Exiting.");
        }

        private static string Normalize(string filename)
        {
            return Path.GetFileName(filename);
        }

        private async Task DownloadTheTranslations(string targetFolder)
        {
            System.Threading.SemaphoreSlim semaphore = new(100);
            List<Task> downloads = new();
            await foreach (var blobItem in ContainerClient_target.GetBlobsAsync())
            {
                await semaphore.WaitAsync();
                BlobClient blobClient = new(StorageConnectionString, ContainerClient_target.Name, blobItem.Name);
                BlobDownloadInfo blobDownloadInfo = await blobClient.DownloadAsync();
                using (FileStream downloadFileStream = File.OpenWrite(targetFolder + Path.DirectorySeparatorChar + blobItem.Name))
                {
                    Task download = blobDownloadInfo.Content.CopyToAsync(downloadFileStream);
                    downloads.Add(download);
                    Debug.WriteLine("Downloaded: " + downloadFileStream.Name);
                    downloadFileStream.Close();
                }
                blobClient.DeleteAsync();
                semaphore.Release();
            }
            await Task.WhenAll(downloads);
            Debug.WriteLine("Download complete.");
            semaphore.Dispose();
            return;
        }

        /// <summary>
        /// Delete the containers. This deletes all containers that start with "doctr" and and with "src", "tgt" or "gls", in this storage account.
        /// In order to clean up from abandoned runs.
        /// </summary>
        /// <returns>Task</returns>
        private async Task DeleteTheContainers(bool CleanUpAll = false)
        {
            List<Task> deletionTasks = new();
            if (CleanUpAll)
            {
                BlobServiceClient blobServiceClient = new(StorageConnectionString);
                var resultSegment = blobServiceClient.GetBlobContainersAsync(BlobContainerTraits.None, BlobContainerStates.None, "doctr").AsPages();
                await foreach (Azure.Page<BlobContainerItem> containerPage in resultSegment)
                {
                    foreach (var containerItem in containerPage.Values)
                    {
                        BlobContainerClient client = new BlobContainerClient(StorageConnectionString, containerItem.Name);
                        if (containerItem.Name.EndsWith("src")
                            || (containerItem.Name.EndsWith("tgt"))
                            || (containerItem.Name.EndsWith("gls")))
                        {
                            deletionTasks.Add(client.DeleteAsync());
                        }
                    }
                }
            }
            else
            {
                deletionTasks.Add(ContainerClient_source.DeleteAsync());
                deletionTasks.Add(ContainerClient_target.DeleteAsync());
                deletionTasks.Add(ContainerClient_glossary.DeleteAsync());
            }
            await Task.WhenAll(deletionTasks);
            Debug.WriteLine("Containers deleted.");
        }

        /// <summary>
        /// Retrieve the status of the translation progress.
        /// </summary>
        /// <param name="processingLocation"></param>
        /// <returns></returns>
        private async Task<StatusResponse> CheckStatus(string processingLocation)
        {
            using HttpClient client = new();
            using HttpRequestMessage request = new()
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(processingLocation)
            };
            request.Headers.Add("Ocp-Apim-Subscription-Key", SubscriptionKey);
            HttpResponseMessage response = await client.SendAsync(request);
            string result = await response.Content.ReadAsStringAsync();
            StatusResponse statusResponse = JsonSerializer.Deserialize<StatusResponse>(result, new JsonSerializerOptions { IncludeFields = true });
            Debug.WriteLine("CheckStatus: Status: " + statusResponse.status);
            Debug.WriteLine("Status Result: "+ result.ToString());
            return statusResponse;
        }

        /// <summary>
        /// Format and submit the translation request to the Document Translation Service. 
        /// </summary>
        /// <param name="input">An object defining the input of what to translate</param>
        /// <returns>The status URL</returns>
        private async Task<string> SubmitTranslationRequest(DocumentTranslationInput input)
        {
            List<DocumentTranslationInput> documentTranslationInputs = new() { input };
            DocumentTranslationRequest documentTranslationRequest = new() { inputs = documentTranslationInputs };

            string requestJson = JsonSerializer.Serialize(documentTranslationRequest, new JsonSerializerOptions() { IncludeFields = true });
            Debug.WriteLine("SubmitTranslationRequest: RequestJson: " + requestJson);
            using HttpRequestMessage request = new();
            request.Method = HttpMethod.Post;
            request.RequestUri = new Uri("https://" + AzureResourceName + baseUriTemplate);
            request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            request.Headers.Add("Ocp-Apim-Subscription-Key", SubscriptionKey);

            using HttpClient client = new();
            HttpResponseMessage response = await client.SendAsync(request);
            Debug.WriteLine("Translation Request response code: " + response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                if (response.Headers.TryGetValues("Operation-Location", out IEnumerable<string> values))
                {
                    return values.First();
                }
            }
            return null;
        }

        #endregion Methods
    }

    #region Helperclasses
    
    public class StatusResponse
    {
        public string id;
        public string createdDateTimeUtc;
        public string lastActionDateTimeUtc;
        public string status;
        public Error error;
        public Summary summary;
    }
    public class Summary
    {
        public int total;
        public int failed;
        public int success;
        public int inProgress;
        public int notYetStarted;
        public int cancelled;
        public int totalCharacterCharged;
    }

    public class Error
    {
        public string code;
        public string message;
        public string target;
        public InnerError innerError;
    }

    public class InnerError
    {
        public string code;
        public string message;
    }
    
    public class DocumentTranslationInput
    {
        public string storageType;
        public DocumentTranslationSource source;
        public List<DocumentTranslationTarget> targets;
    }

    public class DocumentTranslationSource
    {
        public string SourceUrl { get; set; }
    }
        
    public class DocumentTranslationTarget
    {
        public string language { get; set; }
        public string targetUrl { get; set; }
    }

    public class DocumentTranslationRequest
    {
        public List<DocumentTranslationInput> inputs;
    }
        
    /// <summary>
    /// Class for the events generated during a document translation run
    /// </summary>
    public class DocumentTranslationEventArgs : EventArgs
    {
        internal DocumentTranslationEventArgs(StatusResponse statusResponse)
        {
            this.status = statusResponse;
        }

        public StatusResponse status;
    }
    #endregion Helperclasses
}
