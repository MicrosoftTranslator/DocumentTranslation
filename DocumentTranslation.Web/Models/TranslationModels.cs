namespace DocumentTranslation.Web.Models
{
    public class TextTranslationRequest
    {
        public string Text { get; set; } = string.Empty;
        public string FromLanguage { get; set; } = string.Empty;
        public string ToLanguage { get; set; } = string.Empty;
        public string? Category { get; set; }
    }

    public class TextTranslationResponse
    {
        public string TranslatedText { get; set; } = string.Empty;
        public string FromLanguage { get; set; } = string.Empty;
        public string ToLanguage { get; set; } = string.Empty;
    }

    public class DocumentTranslationRequest
    {
        public IFormFile File { get; set; } = null!;
        public string FromLanguage { get; set; } = string.Empty;
        public string ToLanguage { get; set; } = string.Empty;
        public string? Category { get; set; }
    }

    public class DocumentTranslationResponse
    {
        public string TranslatedDocumentUrl { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string FromLanguage { get; set; } = string.Empty;
        public string ToLanguage { get; set; } = string.Empty;
    }

    public class BatchTranslationRequest
    {
        public IFormFile[] Files { get; set; } = Array.Empty<IFormFile>();
        public string FromLanguage { get; set; } = string.Empty;
        public string ToLanguage { get; set; } = string.Empty;
        public string? Category { get; set; }
    }

    public class BatchTranslationResponse
    {
        public string OperationId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int FileCount { get; set; }
    }

    public class TranslationStatusResponse
    {
        public string OperationId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Progress { get; set; }
        public int CompletedDocuments { get; set; }
        public int FailedDocuments { get; set; }
        public int TotalDocuments { get; set; }
    }
}
