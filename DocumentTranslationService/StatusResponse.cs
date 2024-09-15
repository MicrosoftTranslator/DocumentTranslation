using Azure.AI.Translation.Document;

namespace DocumentTranslationService.Core
{
    #region Helperclasses

    public class StatusResponse
    {
        public DocumentTranslationOperation Status;
        public string Message;

        public StatusResponse(DocumentTranslationOperation documentTranslationOperation, string message = null)
        {
            Status = documentTranslationOperation;
            Message = message;
        }
    }
    #endregion Helperclasses
}

