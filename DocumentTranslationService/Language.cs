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
        /// Holds the set of languages. If list is empty, call GetLanguagesAsync first. 
        /// </summary>
        public Dictionary<string, Language> Languages { get; private set; } = new();

        /// <summary>
        /// Fires when the 'Languages' list finished updating. 
        /// </summary>
        public event EventHandler OnLanguagesUpdate;
        public bool ShowExperimental { get; set; }

        private bool? lastShowExperimental = null;
        private string lastLanguage;

        /// <summary>
        /// Read the set of languages from the service and store in the Languages list.
        /// This function can run before any authentication is set up, because the server side does not need authentication for this call.
        /// </summary>
        /// <param name="acceptLanguage">The language you want the language list in. Default is the thread locale</param>
        /// <returns>Task</returns>
        public async Task GetLanguagesAsync(string acceptLanguage = null)
        {
            await GetLanguagesAsyncInternal(acceptLanguage, false);
            if (ShowExperimental)
            {
                Dictionary<string, Language> nonExpLangs = new(Languages);
                await GetLanguagesAsyncInternal(acceptLanguage, true);
                foreach (var lang in Languages)
                    if (nonExpLangs.ContainsKey(lang.Key)) lang.Value.Experimental = false;
                    else lang.Value.Experimental = true;
            }
            OnLanguagesUpdate?.Invoke(this, EventArgs.Empty);
            return;
        }


        /// <summary>
        /// Read the set of languages from the service and store in the Languages list.
        /// This function can run before any authentication is set up, because the server side does not need authentication for this call.
        /// </summary>
        /// <param name="acceptLanguage">The language you want the language list in. Default is the thread locale</param>
        /// <param name="showExperimental">Whether to show the experimental languages</param>
        /// <returns>Task</returns>
        private async Task GetLanguagesAsyncInternal(string acceptLanguage = null, bool showExperimental = false)
        {
            if (string.IsNullOrEmpty(acceptLanguage)) acceptLanguage = System.Threading.Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName;
            //Cut this call short if we have everything and no change in language of the language names, or in the experimental state.
            if ((acceptLanguage == lastLanguage) && (showExperimental == lastShowExperimental) && (Languages.Count > 10)) return;
            lastLanguage = acceptLanguage;
            lastShowExperimental = showExperimental;
            string textTransUri = TextTransUri;
            for (int i = 0; i < 3; i++) //retry loop
            {
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Get
                };
                request.Headers.AcceptLanguage.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue(acceptLanguage));
                request.RequestUri = showExperimental
                    ? new Uri($"{textTransUri}/languages?api-version=3.0&scope=translation&flight=experimental")
                    : new Uri($"{textTransUri}/languages?api-version=3.0&scope=translation");
                HttpClient client = HttpClientFactory.GetHttpClient();
                HttpResponseMessage response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    Languages.Clear();
                    string resultJson = await response.Content.ReadAsStringAsync();
                    using JsonDocument doc = JsonDocument.Parse(resultJson);
                    var langprop = doc.RootElement.GetProperty("translation");
                    foreach (var item in langprop.EnumerateObject())
                    {
                        Language langEntry = new(null, null);
                        var langCode = item.Name;
                        langEntry.LangCode = langCode;
                        foreach (var prop in item.Value.EnumerateObject())
                        {
                            string n = prop.Name;
                            string v = prop.Value.GetString();
                            switch (n)
                            {
                                case "name":
                                    langEntry.Name = v;
                                    break;
                                case "nativeName":
                                    langEntry.NativeName = v;
                                    break;
                                case "dir":
                                    { if (v == "rtl") langEntry.Bidi = true; else langEntry.Bidi = false; }
                                    break;
                                default:
                                    break;
                            }
                        }
                        if (!Languages.TryAdd(langCode, langEntry))
                            Debug.WriteLine($"Duplicate language entry: {langCode}");
                    }
                    Debug.WriteLine($"Languages received: {Languages.Count}, Experimental: {showExperimental}");
                    return;
                }
                else if (response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Unauthorized)
                    textTransUri = "https://api.cognitive.microsofttranslator.us/"; //try Azure Gov in case Azure public is blocked. 
                else await Task.Delay(1000); //wait one seconds before retry
            }
            return;
        }
    }

    /// <summary>
    /// Holds information about a language
    /// </summary>
    public class Language : ICloneable
    {
        public Language(string langCode, string name)
        {
            LangCode = langCode;
            Name = name;
        }

        /// <summary>
        /// ISO639 language code
        /// </summary>
        public string LangCode { get; set; }
        /// <summary>
        /// Friendly name of the language in the language of the Accept-Language setting
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Name of the language in its own language
        /// </summary>
        public string NativeName { get; set; }
        /// <summary>
        /// Is this a bidirectional language?
        /// </summary>
        public bool Bidi { get; set; }

        /// <summary>
        /// Indicates whether this language has been selected, for multi-language operations
        /// </summary>
        public bool IsChecked { get; set; }

        /// <summary>
        /// Is this an experimental language? 
        /// </summary>
        public bool Experimental;

        public object Clone()
        {
            return (Language)MemberwiseClone();
        }
    }
}
