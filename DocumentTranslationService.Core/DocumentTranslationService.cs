using Azure.Storage.Blobs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text.Json;
using System.Net.Http;
using System.Text;
using System.Linq;
using Azure.AI.Translation.Document;
using System.Threading;

namespace DocumentTranslationService.Core
{
    public partial class DocumentTranslationService
    {
        #region Properties
        /// <summary>
        /// The "Connection String" of the Azure blob storage resource. Get from properties of Azure storage.
        /// </summary>
        public string StorageConnectionString { get; } = string.Empty;

        /// <summary>
        /// Holds the Custom Translator category.
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Your Azure Translator subscription key. Get from properties of the Translator resource
        /// </summary>
        public string SubscriptionKey { get; } = string.Empty;

        /// <summary>
        /// The region of your Translator subscription.
        /// Needed only for text translation; can remain empty for document translation.
        /// </summary>
        public string AzureRegion { get; set; }

        /// <summary>
        /// The name of the Azure Translator resource
        /// </summary>
        public string AzureResourceName { get; } = string.Empty;

        internal BlobContainerClient ContainerClientSource { get; set; }
        internal BlobContainerClient ContainerClientTarget { get; set; }
        public DocumentTranslationOperation DocumentTranslationOperation { get => documentTranslationOperation; set => documentTranslationOperation = value; }

        private readonly DocumentTranslationClient documentTranslationClient;

        private DocumentTranslationOperation documentTranslationOperation;

        private CancellationToken cancellationToken;

        private CancellationTokenSource cancellationTokenSource;


        #endregion Properties
        #region Constants
        /// <summary>
        /// The base URL template for making translation requests.
        /// {0} is the name of the Translator resource.
        /// </summary>
        //private const string baseUriTemplate = ".cognitiveservices.azure.com/translator/text/batch/v1.0";
        private const string baseUriTemplate = ".cognitiveservices.azure.com/";
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
            documentTranslationClient = new(new Uri("https://" + AzureResourceName + baseUriTemplate), new Azure.AzureKeyCredential(SubscriptionKey));
        }

        /// <summary>
        /// Fires when initialization is complete.
        /// </summary>
        public event EventHandler OnInitializeComplete;

        /// <summary>
        /// Fills the properties with values from the service. 
        /// </summary>
        /// <returns></returns>
        public async Task InitializeAsync()
        {
            if (String.IsNullOrEmpty(AzureResourceName)) throw new CredentialsException("name");
            if (String.IsNullOrEmpty(SubscriptionKey)) throw new CredentialsException("key");
            List<Task> tasks = new();
            tasks.Add(GetDocumentFormatsAsync());
            tasks.Add(GetGlossaryFormatsAsync());
            tasks.Add(GetLanguagesAsync());
            await Task.WhenAll(tasks);
            if (OnInitializeComplete is not null) OnInitializeComplete(this, EventArgs.Empty);
        }

        /// <summary>
        /// Retrieve the status of the translation progress.
        /// </summary>
        /// <returns></returns>
        public async Task<DocumentTranslationOperation> CheckStatusAsync()
        {
            _ = await documentTranslationOperation.UpdateStatusAsync(cancellationToken);
            return documentTranslationOperation;
        }

        /// <summary>
        /// Cancels an ongoing translation run. 
        /// </summary>
        /// <returns></returns>
        public async Task<Azure.Response> CancelRunAsync()
        {
            cancellationTokenSource.Cancel();
            await documentTranslationOperation.CancelAsync(cancellationToken);
            Azure.Response response = await documentTranslationOperation.UpdateStatusAsync(cancellationToken);
            Debug.WriteLine($"Cancellation: {response.Status} {response.ReasonPhrase}");
            return response;
        }


        /// <summary>
        /// Submit the translation request to the Document Translation Service. 
        /// </summary>
        /// <param name="input">An object defining the input of what to translate</param>
        /// <returns>The status ID</returns>
        public async Task<string> SubmitTranslationRequestAsync(DocumentTranslationInput input)
        {
            if (String.IsNullOrEmpty(AzureResourceName)) throw new CredentialsException("name");
            if (String.IsNullOrEmpty(SubscriptionKey)) throw new CredentialsException("key");
            if (String.IsNullOrEmpty(StorageConnectionString)) throw new CredentialsException("storage");
            cancellationTokenSource = new();
            cancellationToken = cancellationTokenSource.Token;
            try
            {
                documentTranslationOperation = await documentTranslationClient.StartTranslationAsync(input, cancellationToken);
            }
            catch (Azure.RequestFailedException ex)
            {
                Debug.WriteLine("Request failed: " + ex.Source + ": " + ex.Message);
                throw;
            }
            Debug.WriteLine("Translation Request submitted. Status: " + documentTranslationOperation.Status);
            return documentTranslationOperation.Id;
        }

        #endregion Methods
    }
}

