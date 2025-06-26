using DocumentTranslationService.Core;
using DocumentTranslationService.LocalFormats;
using Azure.AI.Translation.Document;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
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
        Task<(Stream fileStream, string fileName, string contentType)> DownloadTranslatedDocumentAsync(string documentId);
    }

    public class DocumentTranslationWebService : IDocumentTranslationWebService
    {
        private readonly DocumentTranslationService.Core.DocumentTranslationService _translationService;
        private readonly DocumentTranslationBusiness _translationBusiness;
        private readonly ILogger<DocumentTranslationWebService> _logger;
        private readonly DocumentTranslationSettings _settings;
        private readonly BlobServiceClient? _blobServiceClient;
        private readonly Dictionary<string, (string blobName, string originalFileName)> _documentStore;
        private const string TranslatedContainerName = "translated";

        public DocumentTranslationWebService(
            IOptions<DocumentTranslationSettings> settings,
            ILogger<DocumentTranslationWebService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
            _documentStore = new Dictionary<string, (string blobName, string originalFileName)>();

            try
            {
                // Initialize Azure Blob Storage client
                if (!string.IsNullOrEmpty(_settings.StorageConnectionString))
                {
                    _blobServiceClient = new BlobServiceClient(_settings.StorageConnectionString);
                    _ = Task.Run(async () => await EnsureContainerExistsAsync());
                }

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

        private async Task EnsureContainerExistsAsync()
        {
            try
            {
                if (_blobServiceClient != null)
                {
                    var containerClient = _blobServiceClient.GetBlobContainerClient(TranslatedContainerName);
                    await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
                    _logger.LogInformation("Blob container '{ContainerName}' ensured to exist", TranslatedContainerName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to ensure blob container exists. Will fall back to local storage.");
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

                // If no category is provided, use the default from settings
                if (string.IsNullOrEmpty(category))
                {
                    category = string.IsNullOrEmpty(_settings.Category) ? _settings.Category : "general"; // Use default category if not provided
                }

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
                    targetFolder);                // Find the translated file
                var translatedFiles = Directory.GetFiles(targetFolder, "*", SearchOption.AllDirectories);
                if (translatedFiles.Length > 0)
                {
                    var translatedFile = translatedFiles[0];
                    
                    // Generate a unique document ID for tracking
                    var documentId = Guid.NewGuid().ToString();
                    var originalFileName = Path.GetFileName(file.FileName);
                    var translatedFileName = Path.GetFileName(translatedFile);
                    
                    // Upload to Azure Blob Storage
                    if (_blobServiceClient != null)
                    {
                        try
                        {
                            var blobName = await UploadToBlobStorageAsync(translatedFile, documentId, translatedFileName);
                            
                            // Store the blob name and original filename for later retrieval
                            _documentStore[documentId] = (blobName, translatedFileName);
                            
                            _logger.LogInformation("Document translated and uploaded to blob storage. DocumentId: {DocumentId}, BlobName: {BlobName}", 
                                documentId, blobName);
                                
                            // Clean up temp files
                            try
                            {
                                Directory.Delete(tempDir, true);
                            }
                            catch (Exception cleanupEx)
                            {
                                _logger.LogWarning(cleanupEx, "Failed to clean up temp directory: {TempDir}", tempDir);
                            }
                            
                            return documentId;
                        }
                        catch (Exception blobEx)
                        {
                            _logger.LogWarning(blobEx, "Failed to upload to blob storage, falling back to local storage");
                            
                            // Fallback to returning local file path if blob storage fails
                            _documentStore[documentId] = (translatedFile, translatedFileName);
                            return documentId;
                        }
                    }
                    else
                    {
                        // No blob storage configured, store locally
                        _documentStore[documentId] = (translatedFile, translatedFileName);
                        _logger.LogInformation("Document translated (local storage). DocumentId: {DocumentId}, FilePath: {FilePath}", 
                            documentId, translatedFile);
                        return documentId;
                    }
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

        private async Task<string> UploadToBlobStorageAsync(string filePath, string documentId, string fileName)
        {
            try
            {
                if (_blobServiceClient == null)
                    throw new InvalidOperationException("Blob service client is not initialized");
                    
                var containerClient = _blobServiceClient.GetBlobContainerClient(TranslatedContainerName);
                
                // Create a unique blob name using the document ID and timestamp
                var fileExtension = Path.GetExtension(fileName);
                var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                var blobName = $"{documentId}_{timestamp}_{fileName}";
                
                var blobClient = containerClient.GetBlobClient(blobName);
                
                // Set appropriate content type and headers
                var blobHttpHeaders = new BlobHttpHeaders
                {
                    ContentType = GetContentType(fileName),
                    ContentDisposition = $"attachment; filename=\"{fileName}\""
                };
                
                // Upload the file to blob storage with retry logic
                using (var fileStream = File.OpenRead(filePath))
                {
                    await blobClient.UploadAsync(fileStream, new BlobUploadOptions
                    {
                        HttpHeaders = blobHttpHeaders,
                        Conditions = null, // Allow overwrite
                        AccessTier = AccessTier.Hot // Use Hot tier for frequently accessed files
                    });
                }
                
                _logger.LogInformation("File successfully uploaded to blob storage: {BlobName}", blobName);
                return blobName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload file to blob storage for document {DocumentId}", documentId);
                throw;
            }
        }

        private string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".doc" => "application/msword",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".xls" => "application/vnd.ms-excel",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".ppt" => "application/vnd.ms-powerpoint",
                ".txt" => "text/plain",
                ".html" => "text/html",
                ".htm" => "text/html",
                ".rtf" => "application/rtf",
                ".odt" => "application/vnd.oasis.opendocument.text",
                ".ods" => "application/vnd.oasis.opendocument.spreadsheet",
                ".odp" => "application/vnd.oasis.opendocument.presentation",
                _ => "application/octet-stream"
            };
        }

        public async Task<(Stream fileStream, string fileName, string contentType)> DownloadTranslatedDocumentAsync(string documentId)
        {
            try
            {
                if (string.IsNullOrEmpty(documentId))
                    throw new ArgumentException("Document ID is required");

                if (!_documentStore.TryGetValue(documentId, out var documentInfo))
                    throw new FileNotFoundException($"Document with ID {documentId} not found");

                Stream fileStream;
                var fileName = documentInfo.originalFileName;
                var contentType = GetContentType(fileName);

                // Try to download from Blob Storage first
                if (_blobServiceClient != null && !Path.IsPathRooted(documentInfo.blobName))
                {
                    try
                    {
                        var containerClient = _blobServiceClient.GetBlobContainerClient(TranslatedContainerName);
                        var blobClient = containerClient.GetBlobClient(documentInfo.blobName);
                        
                        var blobDownloadInfo = await blobClient.DownloadAsync();
                        fileStream = blobDownloadInfo.Value.Content;
                        
                        _logger.LogInformation("Document downloaded from blob storage. DocumentId: {DocumentId}, BlobName: {BlobName}", 
                            documentId, documentInfo.blobName);
                    }
                    catch (Exception blobEx)
                    {
                        _logger.LogWarning(blobEx, "Failed to download from blob storage, trying local fallback for DocumentId: {DocumentId}", documentId);
                        
                        // Fallback to local file if blob storage fails
                        if (File.Exists(documentInfo.blobName))
                        {
                            fileStream = new FileStream(documentInfo.blobName, FileMode.Open, FileAccess.Read);
                        }
                        else
                        {
                            throw new FileNotFoundException($"Document file not found in both blob storage and local storage: {documentId}");
                        }
                    }
                }
                else
                {
                    // Local file fallback (when blobName is actually a file path)
                    if (!File.Exists(documentInfo.blobName))
                        throw new FileNotFoundException($"Document file not found: {documentInfo.blobName}");

                    fileStream = new FileStream(documentInfo.blobName, FileMode.Open, FileAccess.Read);
                    
                    _logger.LogInformation("Document downloaded from local storage. DocumentId: {DocumentId}, FileName: {FileName}", 
                        documentId, fileName);
                }

                return (fileStream, fileName, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download document {DocumentId}", documentId);
                throw;
            }
        }
    }
}
