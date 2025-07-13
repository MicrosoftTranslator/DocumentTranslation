using DocumentTranslation.Web.Models;
using DocumentTranslation.Web.Services;
using DocumentTranslationService.Core;
using DocumentTranslationService.LocalFormats;
using Microsoft.AspNetCore.Mvc;

namespace DocumentTranslation.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TranslationController : ControllerBase
    {
        private readonly IDocumentTranslationWebService _translationService;
        private readonly ILogger<TranslationController> _logger;

        public TranslationController(
            IDocumentTranslationWebService translationService,
            ILogger<TranslationController> logger)
        {
            _translationService = translationService;
            _logger = logger;
        }

        [HttpGet("languages")]
        public async Task<ActionResult<IEnumerable<Language>>> GetLanguages()
        {
            try
            {
                var languages = await _translationService.GetLanguagesAsync();
                return Ok(languages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting languages");
                return StatusCode(500, new { error = "Failed to retrieve languages" });
            }
        }

        [HttpGet("formats")]
        public async Task<ActionResult<IEnumerable<LocalDocumentTranslationFileFormat>>> GetSupportedFormats()
        {
            try
            {
                var formats = await _translationService.GetSupportedFormatsAsync();
                return Ok(formats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting supported formats");
                return StatusCode(500, new { error = "Failed to retrieve supported formats" });
            }
        }

        [HttpPost("text")]
        public async Task<ActionResult<TextTranslationResponse>> TranslateText([FromBody] TextTranslationRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Text))
                    return BadRequest(new { error = "Text is required" });

                if (string.IsNullOrEmpty(request.FromLanguage) || string.IsNullOrEmpty(request.ToLanguage))
                    return BadRequest(new { error = "From and To languages are required" });

                var result = await _translationService.TranslateTextAsync(
                    request.Text, 
                    request.FromLanguage, 
                    request.ToLanguage, 
                    request.Category);

                return Ok(new TextTranslationResponse
                {
                    TranslatedText = result,
                    FromLanguage = request.FromLanguage,
                    ToLanguage = request.ToLanguage
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error translating text");
                return StatusCode(500, new { error = "Failed to translate text" });
            }
        }

        [HttpPost("document")]
        public async Task<ActionResult<DocumentTranslationResponse>> TranslateDocument([FromForm] DocumentTranslationRequest request)
        {
            try
            {
                if (request.File == null || request.File.Length == 0)
                    return BadRequest(new { error = "File is required" });

                if (string.IsNullOrEmpty(request.FromLanguage) || string.IsNullOrEmpty(request.ToLanguage))
                    return BadRequest(new { error = "From and To languages are required" });

                var result = await _translationService.TranslateDocumentAsync(
                    request.File,
                    request.FromLanguage,
                    request.ToLanguage,
                    request.Category);

                // Generate SAS URL for direct download
                var sasUrl = await _translationService.GenerateDocumentSasUrlAsync(result, 5); // 5 minutes expiration

                return Ok(new DocumentTranslationResponse
                {
                    TranslatedDocumentUrl = sasUrl,
                    OriginalFileName = request.File.FileName,
                    FromLanguage = request.FromLanguage,
                    ToLanguage = request.ToLanguage
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error translating document");
                return StatusCode(500, new { error = "Failed to translate document" });
            }
        }

        [HttpPost("batch")]
        public async Task<ActionResult<BatchTranslationResponse>> StartBatchTranslation([FromForm] BatchTranslationRequest request)
        {
            try
            {
                if (request.Files == null || !request.Files.Any())
                    return BadRequest(new { error = "At least one file is required" });

                if (string.IsNullOrEmpty(request.FromLanguage) || string.IsNullOrEmpty(request.ToLanguage))
                    return BadRequest(new { error = "From and To languages are required" });

                var operation = await _translationService.StartBatchTranslationAsync(
                    request.Files.ToList(),
                    request.FromLanguage,
                    request.ToLanguage,
                    request.Category);

                return Ok(new BatchTranslationResponse
                {
                    OperationId = operation.Id,
                    Status = operation.Status.ToString(),
                    FileCount = request.Files.Count()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting batch translation");
                return StatusCode(500, new { error = "Failed to start batch translation" });
            }
        }

        [HttpGet("status/{operationId}")]
        public async Task<ActionResult<TranslationStatusResponse>> GetTranslationStatus(string operationId)
        {
            try
            {
                var status = await _translationService.GetTranslationStatusAsync(operationId);
                
                return Ok(new TranslationStatusResponse
                {
                    OperationId = operationId,
                    Status = status.Status.Status.ToString(),
                    Progress = 100, // Progress based on completion percentage
                    CompletedDocuments = status.Status.DocumentsSucceeded,
                    FailedDocuments = status.Status.DocumentsFailed,
                    TotalDocuments = status.Status.DocumentsTotal
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting translation status for operation {OperationId}", operationId);
                return StatusCode(500, new { error = "Failed to get translation status" });
            }
        }

        [HttpGet("sas/{documentId}")]
        public async Task<ActionResult<object>> GenerateDocumentSasUrl(string documentId)
        {
            try
            {
                if (string.IsNullOrEmpty(documentId))
                {
                    _logger.LogWarning("SAS URL request with empty document ID");
                    return BadRequest(new { error = "Document ID is required" });
                }

                _logger.LogInformation("SAS URL request for document ID: {DocumentId}", documentId);

                var sasUrl = await _translationService.GenerateDocumentSasUrlAsync(documentId, 5); // 5 minutes expiration

                _logger.LogInformation("Generated SAS URL for document {DocumentId}", documentId);

                return Ok(new { sasUrl = sasUrl, expiresInMinutes = 5 });
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogWarning("Document not found for SAS generation: {DocumentId} - {Message}", documentId, ex.Message);
                return NotFound(new { error = "Document not found", documentId = documentId });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid document ID for SAS generation: {DocumentId} - {Message}", documentId, ex.Message);
                return BadRequest(new { error = ex.Message, documentId = documentId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating SAS URL for document {DocumentId}", documentId);
                return StatusCode(500, new { error = "Failed to generate SAS URL", documentId = documentId });
            }
        }
    }
}
