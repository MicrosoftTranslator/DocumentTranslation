using Azure.Storage.Blobs;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DocumentTranslationService.Core
{
    partial class DocumentTranslationService
    {

        /// <summary>
        /// Validate the credentials supplied as properties, and throw a CredentialsExceptions for the failing ones. 
        /// This is meant to be lightweight, but it is not free in terms of time and resources: The method makes
        /// the simplest possible test call to observe the result. 
        /// Use this in a try block and check the value of the Exception to see which credential failed. 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="CredentialsException">
        /// <seealso cref="CredentialsExceptionReason"/>
        /// </exception>
        public async Task TryCredentials()
        {
            List<Task> credTestTasks = new()
            {
                //Test the resource key
                TryCredentialsKey(SubscriptionKey, AzureRegion, TextTransUri),
                //Test the name of the resource
                TryCredentialsName(),
                //Test the storage account
                TryCredentialsStorage()
            };
            await Task.WhenAll(credTestTasks);
            //Test for free subscription
            await TryPaidSubscription();
        }

        private async Task TryCredentialsStorage()
        {
            string containerNameBase = "doctr" + Guid.NewGuid().ToString();
            try
            {
                BlobContainerClient testContainer = new(StorageConnectionString, containerNameBase + "test");
                await testContainer.CreateAsync();
                await testContainer.DeleteIfExistsAsync();
            }
            catch (Exception ex)
            {
                throw new CredentialsException("Storage: " + ex.Message, ex);
            }
        }

        private async Task TryCredentialsName()
        {
            string DocTransEndpoint;
            if (!AzureResourceName.Contains('.')) DocTransEndpoint = "https://" + AzureResourceName + baseUriTemplate;
            else DocTransEndpoint = AzureResourceName;
            try
            {
                HttpRequestMessage request = new() { Method = HttpMethod.Get, RequestUri = new Uri(DocTransEndpoint + "/documents/formats") };
                HttpClient client = new();
                HttpResponseMessage response;
                response = await client.SendAsync(request);
            }
            catch (HttpRequestException ex)
            {
                throw new CredentialsException("Document Translation Endpoint: " + ex.Message, ex);
            }
            catch (System.UriFormatException ex)
            {
                throw new CredentialsException("Document Translation Endpoint: " + ex.Message, ex);
            }
            catch (Exception ex)
            {
                throw new CredentialsException("Document Translation Endpoint: " + ex.Message, ex);
            }
        }

        private static async Task TryCredentialsKey(string subscriptionKey, string azureRegion, string TextTransUri)
        {
            if (string.IsNullOrEmpty(TextTransUri)) TextTransUri = "https://api.cognitive.microsofttranslator.com";
            using HttpRequestMessage request = new() { Method = HttpMethod.Post, RequestUri = new Uri(TextTransUri + "/detect?api-version=3.0") };
            request.Headers.Add("Ocp-Apim-Subscription-Key", subscriptionKey);
            if (azureRegion?.ToLowerInvariant() != "global") request.Headers.Add("Ocp-Apim-Subscription-Region", azureRegion);
            request.Content = new StringContent("[{ \"Text\": \"English\" }]", Encoding.UTF8, "application/json");
            HttpClient client = HttpClientFactory.GetHttpClient();
            try
            {
                client.Timeout = TimeSpan.FromSeconds(15);
                HttpResponseMessage response = await client.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    throw new CredentialsException("Invalid key, or key does not match region.");
            }
            catch (Exception ex)
            {
                throw new CredentialsException("Text Translation Endpoint: " + ex.Message, ex);
            }
        }

        private async Task TryPaidSubscription()
        {
            string DocTransEndpoint;
            if (!AzureResourceName.Contains('.')) DocTransEndpoint = "https://" + AzureResourceName + baseUriTemplate;
            else DocTransEndpoint = AzureResourceName;
            Azure.AI.Translation.Document.DocumentTranslationClient documentTranslationClient = new(new Uri(DocTransEndpoint), new Azure.AzureKeyCredential(SubscriptionKey));

            try
            {
                var result = await documentTranslationClient.GetSupportedDocumentFormatsAsync();
            }
            catch (Azure.RequestFailedException ex)
            {
                throw new CredentialsException("Subscription Type: " + ex.Message, ex);
            }
            catch (Exception ex)
            {
                throw new CredentialsException("Subscription Type: " + ex.Message, ex);
            }
        }
    }
}
