using System.Collections.Generic;

namespace DocumentTranslationServices.Core
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
        public string SourceUrl;
    }

    public class DocumentTranslationTarget
    {
        public string language;
        public string targetUrl;
    }

    public class DocumentTranslationRequest
    {
        public List<DocumentTranslationInput> inputs;
    }

    #endregion Helperclasses
}

