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
            List<Task> credTestTasks = new();
            //Test the subscription key
            credTestTasks.Add(TryCredentialsKey());
            //Test the name of the resource
            credTestTasks.Add(TryCredentialsName());
            //Test the storage account
            credTestTasks.Add(TryCredentialsStorage());
            await Task.WhenAll(credTestTasks);
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
            catch
            {
                throw new CredentialsException("storage");
            }
        }

        private async Task TryCredentialsName()
        {
            HttpRequestMessage request = new() { Method = HttpMethod.Get, RequestUri = new Uri("https://"+ this.AzureResourceName + baseUriTemplate + "/documents/formats") };
            HttpClient client = new();
            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request);
            }
            catch (HttpRequestException)
            {
                throw new CredentialsException("name");
            }
        }

        private async Task TryCredentialsKey()
        {
            HttpRequestMessage request = new() { Method = HttpMethod.Post, RequestUri = new Uri("https://api.cognitive.microsofttranslator.com/detect?api-version=3.0") };
            request.Headers.Add("Ocp-Apim-Subscription-Key", SubscriptionKey);
            request.Content = new StringContent("[{ \"Text\": \"English\" }]", Encoding.UTF8, "application/json");
            HttpClient client = new();
            HttpResponseMessage response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                throw new CredentialsException("key");
        }
    }
}
