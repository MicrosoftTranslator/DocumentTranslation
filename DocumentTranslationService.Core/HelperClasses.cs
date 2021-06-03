using System.Collections.Generic;

namespace DocumentTranslationService.Core
{
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
        public string Language;
        public string SourceUrl;
    }

    public class DocumentTranslationTarget
    {
        public string language;
        public string targetUrl;
        public string category;
        public ServiceGlossary[] glossaries;

        /// <summary>
        /// Describe the target characteristics. Set category and glossaries to use as properties, if desired.
        /// </summary>
        /// <param name="language">Language to translate to</param>
        /// <param name="targetUrl">The Azure storage target SAS URL</param>
        public DocumentTranslationTarget(string language, string targetUrl)
        {
            this.language = language;
            this.targetUrl = targetUrl;
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

    public class DocumentTranslationRequest
    {
        public List<DocumentTranslationInput> inputs;
    }

    #endregion Helperclasses
}

