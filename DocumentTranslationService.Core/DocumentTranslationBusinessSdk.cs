using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.AI.Translation.Document;

namespace DocumentTranslationService.Core
{
    public partial class DocumentTranslationBusiness
    {
        public async Task SdkRunAsync(List<string> filestotranslate, string fromlanguage, string tolanguage, List<string> glossaryfiles = null, string targetFolder = null)
        {
            DocumentTranslationClient translationClient = new(new Uri(TranslationService.AzureResourceName), new Azure.AzureKeyCredential(TranslationService.SubscriptionKey));

        }
    }
}
