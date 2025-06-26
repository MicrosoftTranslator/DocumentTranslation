using DocumentTranslationService.Core;
using DocumentTranslationService.LocalFormats;
using Azure.AI.Translation.Document;
using Microsoft.Extensions.Options;

namespace DocumentTranslation.Web.Services
{
    public interface IDocumentTranslationWebService
    {
        Task<List<Language>> GetLanguagesAsync();
        Task<List<LocalDocumentTranslationFileFormat>> GetSupportedFormatsAsync();
        Task<string> TranslateDocumentAsync(IFormFile file, string fromLanguage, string toLanguage, string? category = null);
        Task<string> TranslateTextAsync(string text, string fromLanguage, string toLanguage, string? category = null);
        Task<DocumentTranslationOperation> StartBatchTranslationAsync(List<IFormFile> files, string fromLanguage, string toLanguage, string? category = null);
        Task<StatusResponse> GetTranslationStatusAsync(string operationId);
    }

    public class DocumentTranslationWebService : IDocumentTranslationWebService
    {
        private readonly DocumentTranslationService.Core.DocumentTranslationService _translationService;
        private readonly DocumentTranslationBusiness _translationBusiness;
        private readonly ILogger<DocumentTranslationWebService> _logger;
        private readonly DocumentTranslationSettings _settings;

        public DocumentTranslationWebService(
            IOptions<DocumentTranslationSettings> settings,
            ILogger<DocumentTranslationWebService> logger)
        {
            _settings = settings.Value;
            _logger = logger;

            try
            {
                // Initialize the core translation service
                _translationService = new DocumentTranslationService.Core.DocumentTranslationService(
                    _settings.SubscriptionKey,
                    _settings.AzureResourceName,
                    _settings.StorageConnectionString)
                {
                    Category = _settings.Category,
                    AzureRegion = _settings.AzureRegion,
                    TextTransUri = _settings.TextTransEndpoint,
                    FlightString = _settings.FlightString
                };

                _translationBusiness = new DocumentTranslationBusiness(_translationService);

                _logger.LogInformation("Document Translation Web Service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Document Translation Web Service");
                throw;
            }
        }

        public async Task<List<Language>> GetLanguagesAsync()
        {
            try
            {
                await _translationService.InitializeAsync();
                var languagesOptions = _translationService.Languages.Select(kvp => kvp.Value).ToList();
                return languagesOptions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get languages");
                throw;
            }
        }

        public Task<List<LocalDocumentTranslationFileFormat>> GetSupportedFormatsAsync()
        {
            try
            {
                // Return the local formats that are supported
                return Task.FromResult(LocalFormats.Formats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get supported formats");
                throw;
            }
        }

        public async Task<string> TranslateDocumentAsync(IFormFile file, string fromLanguage, string toLanguage, string? category = null)
        {
            try
            {
                if (file == null || file.Length == 0)
                    throw new ArgumentException("File is required");

                // Create a temporary file to process
                var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);
                
                var fileName = Path.GetFileName(file.FileName);
                var sourceFile = Path.Combine(tempDir, fileName);

                using (var stream = new FileStream(sourceFile, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Set category if provided
                if (!string.IsNullOrEmpty(category))
                {
                    _translationService.Category = category;
                }

                // Use the business layer to translate the document
                var targetFolder = Path.Combine(tempDir, "translated");
                Directory.CreateDirectory(targetFolder);

                await _translationBusiness.RunAsync(
                    new List<string> { sourceFile }, 
                    fromLanguage, 
                    new string[] { toLanguage }, 
                    null, 
                    targetFolder);

                // Find the translated file
                var translatedFiles = Directory.GetFiles(targetFolder, "*", SearchOption.AllDirectories);
                if (translatedFiles.Length > 0)
                {
                    var translatedFile = translatedFiles[0];
                    var content = await File.ReadAllBytesAsync(translatedFile);
                    
                    // In a real implementation, you'd upload this to blob storage and return the URL
                    // For now, we'll return the file path
                    return translatedFile;
                }

                throw new Exception("No translated file was generated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to translate document from {FromLang} to {ToLang}", fromLanguage, toLanguage);
                throw;
            }
        }

        public async Task<string> TranslateTextAsync(string text, string fromLanguage, string toLanguage, string? category = null)
        {
            try
            {
                if (string.IsNullOrEmpty(text))
                    throw new ArgumentException("Text is required");

                // Set category if provided
                if (!string.IsNullOrEmpty(category))
                {
                    _translationService.Category = category;
                }

                var result = await _translationService.TranslateStringAsync(text, fromLanguage, toLanguage);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to translate text from {FromLang} to {ToLang}", fromLanguage, toLanguage);
                throw;
            }
        }

        public async Task<DocumentTranslationOperation> StartBatchTranslationAsync(List<IFormFile> files, string fromLanguage, string toLanguage, string? category = null)
        {
            try
            {
                if (files == null || !files.Any())
                    throw new ArgumentException("At least one file is required");

                var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                var filePaths = new List<string>();

                // Save all files to temp directory
                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file.FileName);
                    var filePath = Path.Combine(tempDir, fileName);
                    
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                    
                    filePaths.Add(filePath);
                }

                // Set category if provided
                if (!string.IsNullOrEmpty(category))
                {
                    _translationService.Category = category;
                }

                // Start batch translation
                var targetFolder = Path.Combine(tempDir, "translated");
                Directory.CreateDirectory(targetFolder);

                await _translationBusiness.RunAsync(
                    filePaths, 
                    fromLanguage, 
                    new string[] { toLanguage }, 
                    null, 
                    targetFolder);

                // Since the original RunAsync is synchronous and doesn't return an operation,
                // we'll return the operation that was stored in the service during the run
                var operation = _translationService.DocumentTranslationOperation;
                if (operation == null)
                {
                    _logger.LogWarning("No DocumentTranslationOperation found after running batch translation");
                    throw new InvalidOperationException("No translation operation was created");
                }
                
                return operation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start batch translation from {FromLang} to {ToLang}", fromLanguage, toLanguage);
                throw;
            }
        }

        public async Task<StatusResponse> GetTranslationStatusAsync(string operationId)
        {
            try
            {
                // Check status using the operation stored in the service
                var operation = await _translationService.CheckStatusAsync();
                return new StatusResponse(operation, "Status retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get translation status for operation {OperationId}", operationId);
                throw;
            }
        }
    }
}
