using Azure.Storage.Blobs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text.Json;
using System.Net.Http;
using System.Text;
using System.Linq;

namespace DocumentTranslationServices.Core
{
    public partial class DocumentTranslationService
    {
        #region Properties
        /// <summary>
        /// The "Connection String" of the Azure blob storage resource. Get from properties of Azure storage.
        /// </summary>
        internal string StorageConnectionString { get; } = string.Empty;

        /// <summary>
        /// Your Azure Translator subscription key. Get from properties of the Translator resource
        /// </summary>
        private string SubscriptionKey { get; } = string.Empty;

        /// <summary>
        /// The name of the Azure Translator resource
        /// </summary>
        public string AzureResourceName { get; } = string.Empty;

        internal string ProcessingLocation { get; set; } = string.Empty;

        internal BlobContainerClient ContainerClientSource { get; set; }
        internal BlobContainerClient ContainerClientTarget { get; set; }
        internal BlobContainerClient ContainerClientGlossary { get; set; }

        #endregion Properties
        #region Constants
        /// <summary>
        /// The base URL template for making translation requests.
        /// {0} is the name of the Translator resource.
        /// </summary>
        private const string baseUriTemplate = ".cognitiveservices.azure.com/translator/text/batch/v1.0-preview.1";
        #endregion Constants
        #region Methods

        /// <summary>
        /// Constructor
        /// </summary>
        public DocumentTranslationService(string SubscriptionKey, string AzureResourceName, string StorageConnectionString)
        {
            this.SubscriptionKey = SubscriptionKey;
            this.AzureResourceName = AzureResourceName;
            this.StorageConnectionString = StorageConnectionString;
        }

        public event EventHandler OnInitializeComplete;

        public async Task Initialize()
        {
            List<Task> tasks = new();
            tasks.Add(GetFormatsAsync());
            tasks.Add(GetGlossaryFormatsAsync());
            tasks.Add(GetLanguagesAsync());
            await Task.WhenAll(tasks);
            if (OnInitializeComplete is not null) OnInitializeComplete(this, EventArgs.Empty);
        }

        /// <summary>
        /// Retrieve the status of the translation progress.
        /// </summary>
        /// <returns></returns>
        public async Task<StatusResponse> CheckStatus()
        {
            using HttpClient client = new();
            using HttpRequestMessage request = new() { Method = HttpMethod.Get, RequestUri = new Uri(ProcessingLocation) };
            request.Headers.Add("Ocp-Apim-Subscription-Key", SubscriptionKey);
            HttpResponseMessage response = await client.SendAsync(request);
            string result = await response.Content.ReadAsStringAsync();
            StatusResponse statusResponse = JsonSerializer.Deserialize<StatusResponse>(result, new JsonSerializerOptions { IncludeFields = true });
            Debug.WriteLine("CheckStatus: Status: " + statusResponse.status);
            Debug.WriteLine("CheckStatus: inProgress: " + statusResponse.summary.inProgress);
            Debug.WriteLine("Status Result: " + result.ToString());
            return statusResponse;
        }

        /// <summary>
        /// Format and submit the translation request to the Document Translation Service. 
        /// </summary>
        /// <param name="input">An object defining the input of what to translate</param>
        /// <returns>The status URL</returns>
        public async Task<string> SubmitTranslationRequest(DocumentTranslationInput input)
        {
            List<DocumentTranslationInput> documentTranslationInputs = new() { input };
            DocumentTranslationRequest documentTranslationRequest = new() { inputs = documentTranslationInputs };

            string requestJson = JsonSerializer.Serialize(documentTranslationRequest, new JsonSerializerOptions() { IncludeFields = true });
            Debug.WriteLine("SubmitTranslationRequest: RequestJson: " + requestJson);
            HttpRequestMessage request = new();
            request.Method = HttpMethod.Post;
            request.RequestUri = new Uri("https://" + AzureResourceName + baseUriTemplate + "/batches");
            request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            request.Headers.Add("Ocp-Apim-Subscription-Key", SubscriptionKey);

            HttpClient client = new();
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
}

