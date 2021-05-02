using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace DocumentTranslationService.Core
{
    public partial class DocumentTranslationService
    {
        /// <summary>
        /// Holds the list of file formats after initial retrieval from Service
        /// </summary>
        public FileFormatList FileFormats { get; private set; }

        public HashSet<string> Extensions { get; private set; } = new();

        public HashSet<string> GlossaryExtensions { get; private set; } = new();

        public event EventHandler OnFileFormatsUpdate;

        public FileFormatList GlossaryFormats { get; private set; }

        public event EventHandler OnGlossaryFormatsUpdate;

        public async Task<FileFormatList> GetFormatsAsync()
        {
            if (FileFormats?.value.Length > 0) return FileFormats;
            else return await GetFormatsInternal();
        }

        private async Task<FileFormatList> GetFormatsInternal()
        {
            for (int i = 0; i < 3; i++)
            {
                HttpRequestMessage request = new();
                request.Method = HttpMethod.Get;
                request.RequestUri = new Uri("https://" + AzureResourceName + baseUriTemplate + "/documents/formats");
                request.Headers.Add("Ocp-Apim-Subscription-Key", SubscriptionKey);

                HttpClient client = new();
                HttpResponseMessage response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string responseJson = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"GetFormats: Response: {responseJson}");
                    FileFormats = JsonSerializer.Deserialize<FileFormatList>(responseJson, new JsonSerializerOptions { IncludeFields = true });
                    foreach (Value item in FileFormats.value)
                    {
                        foreach (string ext in item.fileExtensions)
                        {
                            Extensions.Add(ext.ToLowerInvariant());
                        }
                    }
                    if (OnFileFormatsUpdate is not null) OnFileFormatsUpdate(this, EventArgs.Empty);
                    return FileFormats;
                }
                else
                {
                    Debug.WriteLine("GetFormatsInternal: Get file formats failed.");
                    await Task.Delay(1000);
                }
            }
            return null;
        }

        public async Task<FileFormatList> GetGlossaryFormatsAsync()
        {
            if (GlossaryFormats?.value.Length > 0) return GlossaryFormats;
            else return await GetGlossaryFormatsInternal();
        }

        private async Task<FileFormatList> GetGlossaryFormatsInternal()
        {
            for (int i = 0; i < 3; i++)
            {
                HttpRequestMessage request = new();
                request.Method = HttpMethod.Get;
                request.RequestUri = new Uri("https://" + AzureResourceName + baseUriTemplate + "/glossaries/formats");
                request.Headers.Add("Ocp-Apim-Subscription-Key", SubscriptionKey);

                HttpClient client = new();
                HttpResponseMessage response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string responseJson = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"GetGlossaryFormats: Response: {responseJson}");
                    GlossaryFormats = JsonSerializer.Deserialize<FileFormatList>(responseJson, new JsonSerializerOptions { IncludeFields = true });
                    foreach (Value item in GlossaryFormats.value)
                    {
                        foreach (string ext in item.fileExtensions)
                        {
                            GlossaryExtensions.Add(ext.ToLowerInvariant());
                        }
                    }

                    if (OnGlossaryFormatsUpdate is not null) OnGlossaryFormatsUpdate(this, EventArgs.Empty);
                    return GlossaryFormats;
                }
                else
                {
                    Debug.WriteLine("GetGlossaryFormatsInternal: Get glossary formats failed.");
                    await Task.Delay(1000);
                }
            }
            return null;
        }
    }
}
