using Azure.AI.Translation.Document;
using System.Collections.Generic;

namespace DocumentTranslationService.Core
{
    #region Helperclasses

    public class StatusResponse
    {
        public Azure.AI.Translation.Document.DocumentTranslationOperation Status;

        public StatusResponse(DocumentTranslationOperation documentTranslationOperation)
        {
            Status = documentTranslationOperation;
        }
    }


    public class ServiceGlossary
    {
        public string format;
        public string glossaryUrl;
        public string storageSource;

        public ServiceGlossary(string glossaryUrl, string format)
        {
            this.format = format;
            this.glossaryUrl = glossaryUrl;
            this.storageSource = "AzureBlob";
        }
    }
    #endregion Helperclasses
}

