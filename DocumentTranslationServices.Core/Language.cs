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
        public bool ShowExperimental { get => showExperimental; set => showExperimental = value; }
        private bool showExperimental = false;
        private bool lastShowExperimental = false;

        public Dictionary<String, Language> Languages { get; private set; } = new();

        public event EventHandler OnLanguagesUpdate;

        private string lastLanguage;

        public async Task GetLanguagesAsync(string acceptLanguage = null)
        {
            if (acceptLanguage is null) acceptLanguage = System.Threading.Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName;
            //Cut this call short if we have everything and no change in language of the language names, or in the experimental state.
            if ((acceptLanguage == lastLanguage) && (showExperimental == lastShowExperimental) && (Languages.Count > 10)) return;
            lastLanguage = acceptLanguage;
            lastShowExperimental = showExperimental;
            for (int i = 0; i < 3; i++) //retry loop
            {
                HttpRequestMessage request = new();
                request.Method = HttpMethod.Get;
                request.Headers.AcceptLanguage.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue(acceptLanguage));
                request.RequestUri = showExperimental
                    ? new Uri("https://api.cognitive.microsofttranslator.com/languages?api-version=3.0&scope=translation&flight=experimental")
                    : new Uri("https://api.cognitive.microsofttranslator.com/languages?api-version=3.0&scope=translation");
                HttpClient client = new();
                HttpResponseMessage response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    Languages.Clear();
                    string resultJson = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine("Languages received.");
                    using JsonDocument doc = JsonDocument.Parse(resultJson);
                    var langprop = doc.RootElement.GetProperty("translation");
                    foreach (var item in langprop.EnumerateObject())
                    {
                        Language langEntry = new();
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
                    if (OnLanguagesUpdate is not null) OnLanguagesUpdate(this, EventArgs.Empty);
                    return;
                }
                else await Task.Delay(2000); //wait two seconds before retry
            }
            return;
        }
    }

    /// <summary>
    /// Holds information about a language
    /// </summary>
    public class Language
    {
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
    }


}
