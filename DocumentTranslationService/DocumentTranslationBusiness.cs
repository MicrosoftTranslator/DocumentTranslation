using Azure.AI.Translation.Document;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentTranslationService.Core
{
    public partial class DocumentTranslationBusiness
    {
        #region Properties
        public DocumentTranslationService TranslationService { get; }

        /// <summary>
        /// Can retrieve the final target folder here
        /// </summary>
        public string TargetFolder { get; private set; }

        /// <summary>
        /// Prevent deletion of storage container. For debugging.
        /// </summary>
        public bool Nodelete { get; set; } = false;

        /// <summary>
        /// Fires when errors were encountered in the translation run
        /// The message is a tab-separated list of files names and the error message and code returned from server
        /// </summary>
        public event EventHandler<string> OnThereWereErrors;

        /// <summary>
        /// Fires when final results are available;
        /// Returns the sum of characters translated.
        /// </summary>
        public event EventHandler<long> OnFinalResults;

        /// <summary>
        /// Fires during a translation run when there is an updated status. Approximately once per second. 
        /// </summary>
        public event EventHandler<StatusResponse> OnStatusUpdate;

        /// <summary>
        /// Fires when the translated files completed downloading. Maybe before the Run method exits, due to necessary cleanup work. 
        /// Returns count and total size of the download.
        /// </summary>
        public event EventHandler<(int, long)> OnDownloadComplete;

        /// <summary>
        /// Fires when the source files start uploading.  
        /// Returns count and total size of the download.
        /// </summary>
        public event EventHandler OnUploadStart;


        /// <summary>
        /// Fires when the source files completed uploading.  
        /// Returns count and total size of the download.
        /// </summary>
        public event EventHandler<(int, long)> OnUploadComplete;

        /// <summary>
        /// Fires if there were files listed to translate that were discarded.
        /// </summary>
        public event EventHandler<List<string>> OnFilesDiscarded;

        /// <summary>
        /// Fires if there were glossaries listed that were discarded.
        /// </summary>
        public event EventHandler<List<string>> OnGlossariesDiscarded;

        public event EventHandler<string> OnContainerCreationFailure;

        /// <summary>
        /// Fires if the file could not be read or written.
        /// <para>File name</para>
        /// </summary>
        public event EventHandler<string> OnFileReadWriteError;

        /// <summary>
        /// Fires each time there is a status pull with a response from the service. 
        /// The argument is the http status of the status request
        /// </summary>
        public event EventHandler<int> OnHeartBeat;

        private readonly Logger logger = new();

        /// <summary>
        /// Holds the files used as glossary.
        /// </summary>
        private Glossary glossary;

        #endregion Properties

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="documentTranslationService"></param>
        public DocumentTranslationBusiness(DocumentTranslationService documentTranslationService)
        {
            TranslationService = documentTranslationService;
        }

        /// <summary>
        /// Perform a translation of a set of files using the TranslationService passed in the Constructor.
        /// </summary>
        /// <param name="filestotranslate">A list of files to translate. Can be a single file or a single directory.</param>
        /// <param name="fromlanguage">A single source language. Can be null.</param>
        /// <param name="tolanguage">A single target language.</param>
        /// <param name="glossaryfiles">The glossary files.</param>
        /// <returns></returns>
        public async Task RunAsync(List<string> filestotranslate, string fromlanguage, string[] tolanguages, List<string> glossaryfiles = null, string targetFolder = null)
        {
            Stopwatch stopwatch = new();
            stopwatch.Start();
            logger.WriteLine($"{stopwatch.Elapsed.TotalSeconds} Translation run started");
            if (filestotranslate.Count == 0) throw new ArgumentNullException(nameof(filestotranslate), "No files to translate.");
            Task initialize = TranslationService.InitializeAsync();

            #region Build the list of files to translate
            List<string> sourcefiles = new();
            foreach (string filename in filestotranslate)
            {
                if ((File.GetAttributes(filename) & FileAttributes.Directory) == FileAttributes.Directory)
                    foreach (var file in Directory.EnumerateFiles(filename))
                        sourcefiles.Add(file);
                else sourcefiles.Add(filename);
            }

            await initialize;
            #endregion

            #region Parameter checking
            if (TranslationService.Extensions.Count == 0)
                throw new ArgumentNullException(nameof(TranslationService.Extensions), "List of translatable extensions cannot be null.");
            List<string> discards;
            (sourcefiles, discards) = FilterByExtension(sourcefiles, TranslationService.Extensions);
            if (discards is not null)
            {
                foreach (string fileName in discards)
                {
                    logger.WriteLine($"Discarded due to invalid file format for translation: {fileName}");
                }
                if ((OnFilesDiscarded is not null) && (discards.Count > 0)) OnFilesDiscarded(this, discards);
            }
            if (sourcefiles.Count == 0)
            {
                //There is nothing to translate
                logger.WriteLine("Nothing left to translate.");
                throw new ArgumentNullException(nameof(filestotranslate), "List filtered to nothing.");
            }
            if (!TranslationService.Languages.ContainsKey(tolanguages[0])) throw new ArgumentException("Invalid 'to' language.", nameof(tolanguages));
            #endregion

            List<string> sourcefiles_prep = LocalFormats.LocalFormats.PreprocessSourceFiles(sourcefiles);

            #region Create the containers
            logger.WriteLine($"{stopwatch.Elapsed.TotalSeconds} START - container creation.");
            string containerNameBase = "doctr" + Guid.NewGuid().ToString();
            BlobContainerClient sourceContainer;
            try
            {
                sourceContainer = new(TranslationService.StorageConnectionString, containerNameBase + "src");
            }
            catch (Exception ex)
            {
                logger.WriteLine(ex.Message + ex.InnerException?.Message);
                OnContainerCreationFailure?.Invoke(this, ex.Message);
                return;
            }
            var sourceContainerTask = sourceContainer.CreateIfNotExistsAsync();
            TranslationService.ContainerClientSource = sourceContainer;
            List<Task> targetContainerTasks = new();
            Dictionary<string, BlobContainerClient> targetContainers = new();
            TranslationService.ContainerClientTargets.Clear();
            foreach (string lang in tolanguages)
            {
                BlobContainerClient targetContainer = new(TranslationService.StorageConnectionString, containerNameBase + "tgt" + lang.ToLowerInvariant());
                targetContainerTasks.Add(targetContainer.CreateIfNotExistsAsync());
                TranslationService.ContainerClientTargets.Add(lang, targetContainer);
                targetContainers.Add(lang, targetContainer);
            }
            #endregion

            #region Upload documents
            try
            {
                await sourceContainerTask;
            }
            catch (Exception ex)
            {
                logger.WriteLine(ex.Message + ex.InnerException?.Message);
                OnContainerCreationFailure?.Invoke(this, ex.Message);
                return;
            }
            logger.WriteLine($"{stopwatch.Elapsed.TotalSeconds} END - container creation.");
            logger.WriteLine($"{stopwatch.Elapsed.TotalSeconds} START - Documents and glossaries upload.");
            OnUploadStart?.Invoke(this, EventArgs.Empty);
            int count = 0;
            long sizeInBytes = 0;
            List<Task> uploadTasks = new();
            using (System.Threading.SemaphoreSlim semaphore = new(100))
            {
                foreach (var filename in sourcefiles_prep)
                {
                    await semaphore.WaitAsync();

                    BlobClient blobClient = new(TranslationService.StorageConnectionString, TranslationService.ContainerClientSource.Name, Normalize(filename));
                    try
                    {
                        uploadTasks.Add(blobClient.UploadAsync(filename, true));
                        count++;
                        sizeInBytes += new FileInfo(filename).Length;
                        semaphore.Release();
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or AggregateException or Azure.RequestFailedException)
                    {
                        logger.WriteLine($"Uploading file {filename} failed with {ex.Message}");
                        OnFileReadWriteError?.Invoke(this, ex.Message);
                        if (!Nodelete) await DeleteContainersAsync(tolanguages);
                        return;
                    }
                    logger.WriteLine($"File {filename} upload started.");
                }
            }
            Debug.WriteLine("Awaiting document upload task completion.");
            try
            {
                await Task.WhenAll(uploadTasks);
            }
            catch (Exception ex)
            {
                logger.WriteLine($"Uploading files failed with {ex.Message}");
                OnFileReadWriteError?.Invoke(this, ex.Message);
                if (!Nodelete) await DeleteContainersAsync(tolanguages);
                return;
            }
            //Upload Glossaries
            glossary = new(TranslationService, glossaryfiles);
            glossary.OnGlossaryDiscarded += Glossary_OnGlossaryDiscarded;
            try
            {
                (int, long) result = await glossary.UploadAsync(TranslationService.StorageConnectionString, containerNameBase);
            }
            catch (Exception ex)
            {
                logger.WriteLine($"Glossaries upload failed with {ex.Message}");
                OnFileReadWriteError?.Invoke(this, ex.Message);
                if (!Nodelete) await DeleteContainersAsync(tolanguages);
                return;
            }
            OnUploadComplete?.Invoke(this, (count, sizeInBytes));
            logger.WriteLine($"{stopwatch.Elapsed.TotalSeconds} END - Document upload. {sizeInBytes} bytes in {count} documents.");
            #endregion

            #region Translate the container content
            try
            {
                await Task.WhenAll(targetContainerTasks);
            }
            catch (Exception ex)
            {
                logger.WriteLine($"Target container creation failed with {ex.Message}");
                OnFileReadWriteError?.Invoke(this, ex.Message);
                if (!Nodelete) await DeleteContainersAsync(tolanguages);
                return;
            }

            DocumentTranslationOperation status;
            try
            {
                DocumentTranslationInput input = GenerateInput(fromlanguage, tolanguages, sourceContainer, targetContainers, glossary, false);
                string statusID = await TranslationService.SubmitTranslationRequestAsync(input);
                logger.WriteLine($"{stopwatch.Elapsed.TotalSeconds} START - Translation service request with SAS token. StatusID: {statusID}");
            }
            catch (Azure.RequestFailedException ex)
            {
                OnStatusUpdate?.Invoke(this, new StatusResponse(TranslationService.DocumentTranslationOperation, ex.ErrorCode + ": " + ex.Message));
                logger.WriteLine(ex.ErrorCode + ": " + ex.Message);
            }
            catch (Exception ex)
            {
                OnStatusUpdate?.Invoke(this, new StatusResponse(TranslationService.DocumentTranslationOperation, ex.Message));
                logger.WriteLine(ex.Message);
            }
            if (TranslationService.DocumentTranslationOperation is null)
            {
                logger.WriteLine("ERROR: Start of translation job failed.");
                if (!Nodelete) await DeleteContainersAsync(tolanguages);
                OnThereWereErrors?.Invoke(this, "ERROR: Start of translation job failed with a NULL response from service.");
                return;
            }

            //Check on status until status is in a final state
            DateTimeOffset lastActionTime = DateTimeOffset.MinValue;
            do
            {
                await Task.Delay(1000);
                status = null;
                try
                {
                    status = await TranslationService.CheckStatusAsync();
                }
                catch (Azure.RequestFailedException ex)
                {
                    if (ex.ErrorCode == "InvalidRequest")
                    {
                        //Retry with managed identity URIs:
                        try
                        {
                            //Last parameter, here: true, forces managed identity
                            DocumentTranslationInput input = GenerateInput(fromlanguage, tolanguages, sourceContainer, targetContainers, glossary, true);
                            string statusID = await TranslationService.SubmitTranslationRequestAsync(input);
                            logger.WriteLine($"{stopwatch.Elapsed.TotalSeconds} START - Translation service request with Managed Identity. StatusID: {statusID}");
                            await Task.Delay(200);
                            status = await TranslationService.CheckStatusAsync();
                        }
                        catch (Azure.RequestFailedException ex2)
                        {
                            OnStatusUpdate?.Invoke(this, new StatusResponse(TranslationService.DocumentTranslationOperation, ex2.ErrorCode + ": " + ex2.Message));
                            logger.WriteLine("After trying with SAS token and managed identity URLs: " + ex2.ErrorCode + ": " + ex2.Message);
                            OnThereWereErrors(this, ex2.ErrorCode + "  " + ex2.Message);
                            return;
                        }
                    }
                    else
                    {
                        OnStatusUpdate?.Invoke(this, new StatusResponse(TranslationService.DocumentTranslationOperation, ex.ErrorCode + ": " + ex.Message));
                        logger.WriteLine(ex.ErrorCode + ": " + ex.Message);
                        OnThereWereErrors(this, ex.ErrorCode + "  " + ex.Message);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    OnThereWereErrors(this, ex.Message);
                    logger.WriteLine($"{stopwatch.Elapsed.TotalSeconds} Status: {ex.Message}");
                    return;
                }
                logger.WriteLine($"{stopwatch.Elapsed.TotalSeconds} Http status: {TranslationService.AzureHttpStatus.Status} {TranslationService.AzureHttpStatus.ReasonPhrase}");
                OnHeartBeat?.Invoke(this, TranslationService.AzureHttpStatus.Status);
                if (status is null)
                {
                    logger.WriteLine($"{stopwatch.Elapsed.TotalSeconds} Failed to receive status: Translation run terminated with: {TranslationService.AzureHttpStatus.Status} {TranslationService.AzureHttpStatus.ReasonPhrase}");
                    OnThereWereErrors?.Invoke(this, $"Failed to receive status: Translation run terminated with: {TranslationService.AzureHttpStatus.Status} {TranslationService.AzureHttpStatus.ReasonPhrase}");
                    return;
                }
                logger.WriteLine($"{stopwatch.Elapsed.TotalSeconds} Service status: {status.CreatedOn} {status.Status}");
                if (status.LastModified != lastActionTime)
                {
                    //Raise the update event
                    OnStatusUpdate?.Invoke(this, new StatusResponse(status));
                    lastActionTime = status.LastModified;
                }
            }
            while (
                  (status.DocumentsInProgress != 0)
                || (!status.HasCompleted));
            OnStatusUpdate?.Invoke(this, new StatusResponse(status));
            Task<List<DocumentStatusResult>> finalResultsTask = TranslationService.GetFinalResultsAsync();
            if (status.Status == DocumentTranslationStatus.ValidationFailed) return;
            #endregion

            #region Download the translations
            //Chance for optimization: Check status on the documents and start download immediately after each document is translated. 
            logger.WriteLine($"{stopwatch.Elapsed.TotalSeconds} START - document download.");
            count = 0;
            sizeInBytes = 0;
            foreach (string lang in tolanguages)
            {
                string directoryName;
                if (string.IsNullOrEmpty(targetFolder)) directoryName = Path.GetDirectoryName(sourcefiles[0]) + Path.DirectorySeparatorChar + lang;
                else
                    if (targetFolder.Contains('*')) directoryName = targetFolder.Replace("*", lang);
                else
                        if (tolanguages.Length == 1) directoryName = targetFolder;
                else directoryName = targetFolder + Path.DirectorySeparatorChar + lang;
                DirectoryInfo directory = new(directoryName);
                try
                {
                    directory = Directory.CreateDirectory(directoryName);
                }
                catch (UnauthorizedAccessException ex)
                {
                    logger.WriteLine(ex.Message);
                    OnFileReadWriteError?.Invoke(this, ex.Message);
                    if (!Nodelete) await DeleteContainersAsync(tolanguages);
                    return;
                }
                List<Task> downloads = new();
                using (System.Threading.SemaphoreSlim semaphore = new(100))
                {
                    await foreach (var blobItem in TranslationService.ContainerClientTargets[lang].GetBlobsAsync())
                    {
                        await semaphore.WaitAsync();
                        downloads.Add(DownloadBlobAsync(directory, blobItem, lang));
                        count++;
                        sizeInBytes += (long)blobItem.Properties.ContentLength;
                        semaphore.Release();
                    }
                }
                try
                {
                    await Task.WhenAll(downloads);
                }
                catch (Exception ex)
                {
                    logger.WriteLine("Download error: " + ex.Message);
                    OnFileReadWriteError?.Invoke(this, "Download failure: " + ex.Message);
                }
                this.TargetFolder = directoryName;
            }
            //Process the local file formats after download.
            try
            {
                LocalFormats.LocalFormats.PostprocessTargetFiles(Directory.GetFiles(TargetFolder).ToList());
            }
            catch (Exception ex)
            {
                logger.WriteLine("Postprocessing error: " + ex.Message);
                OnFileReadWriteError?.Invoke(this, "Postprocessing failure: " + ex.Message);
            }

            #endregion
            #region final
            OnDownloadComplete?.Invoke(this, (count, sizeInBytes));
            logger.WriteLine($"{stopwatch.Elapsed.TotalSeconds} END - Documents downloaded: {sizeInBytes} bytes in {count} files.");
            if (!Nodelete) await DeleteContainersAsync(tolanguages);
            var finalResults = await finalResultsTask;
            OnFinalResults?.Invoke(this, CharactersCharged(finalResults));
            StringBuilder sb = new();
            bool thereWereErrors = false;
            foreach (var documentStatus in finalResults)
            {
                if (documentStatus.Error is not null)
                {
                    thereWereErrors = true;
                    sb.Append(ToDisplayForm(documentStatus.SourceDocumentUri.LocalPath) + "\t");
                    sb.Append(documentStatus.TranslatedToLanguageCode + "\t");
                    sb.Append(documentStatus.Error.Message);
                    sb.AppendLine(" (" + documentStatus.Error.Code + ")");
                }
            }
            if (thereWereErrors)
            {
                OnThereWereErrors?.Invoke(this, sb.ToString());
            }
            logger.WriteLine($"{stopwatch.Elapsed.TotalSeconds} Run: Exiting.");
            logger.Close();
            #endregion
        }

        private void Glossary_OnGlossaryDiscarded(object sender, List<string> e)
        {
            OnGlossariesDiscarded?.Invoke(this, e);
        }

        private DocumentTranslationInput GenerateInput(string fromlanguage, string[] tolanguages, BlobContainerClient sourceContainer, Dictionary<string, BlobContainerClient> targetContainers, Glossary glossary, bool UseManagedIdentity)
        {
            Uri sourceUri = GenerateSasUriSource(sourceContainer, UseManagedIdentity);
            TranslationSource translationSource = new(sourceUri);
            logger.WriteLine($"UseManagedIdentity: {UseManagedIdentity}");
            logger.WriteLine($"SourceURI: {sourceUri}");
            if (!(string.IsNullOrEmpty(fromlanguage)))
            {
                if (fromlanguage.ToLowerInvariant() == "auto")
                    translationSource.LanguageCode = null;
                else
                    translationSource.LanguageCode = fromlanguage;
            }

            Dictionary<string, Uri> sasUriTargets = GenerateSasUriTargets(tolanguages, targetContainers, UseManagedIdentity);
            List<TranslationTarget> translationTargets = new();
            foreach (string lang in tolanguages)
            {
                TranslationTarget translationTarget = new(sasUriTargets[lang], lang);
                if (glossary.Glossaries is not null)
                {
                    if (UseManagedIdentity)
                        foreach (var glos in glossary.PlainUriGlossaries) translationTarget.Glossaries.Add(glos.Value);
                    else
                        foreach (var glos in glossary.Glossaries) translationTarget.Glossaries.Add(glos.Value);
                }
                if (TranslationService.Category is not null)
                {
                    translationTarget.CategoryId = TranslationService.Category;
                }
                translationTargets.Add(translationTarget);
            }
            return new(translationSource, translationTargets);
        }

        private Dictionary<string, Uri> GenerateSasUriTargets(string[] tolanguages, Dictionary<string, BlobContainerClient> targetContainers, bool UseManagedIdentity)
        {
            Dictionary<string, Uri> sasUriTargets = new();
            foreach (string lang in tolanguages)
            {
                if (UseManagedIdentity)
                    sasUriTargets.Add(lang, targetContainers[lang].Uri);
                else
                    sasUriTargets.Add(lang, targetContainers[lang].GenerateSasUri(BlobContainerSasPermissions.Write | BlobContainerSasPermissions.List, DateTimeOffset.UtcNow + TimeSpan.FromHours(5)));
                logger.WriteLine($"TargetURI: {sasUriTargets[lang]}");
            }

            return sasUriTargets;
        }

        private static Uri GenerateSasUriSource(BlobContainerClient sourceContainer, bool UseManagedIdentity)
        {
            if (UseManagedIdentity)
                return sourceContainer.Uri;
            else
                return sourceContainer.GenerateSasUri(BlobContainerSasPermissions.Read | BlobContainerSasPermissions.List, DateTimeOffset.UtcNow + TimeSpan.FromHours(5));
        }

        private static string ToDisplayForm(string localPath)
        {
            string[] splits = localPath.Split('/');
            return splits[^1];
        }

        private long CharactersCharged(List<DocumentStatusResult> finalResults)
        {
            long characterscharged = 0;
            foreach (var result in finalResults)
            {
                characterscharged += result.CharactersCharged;
            }
            logger.WriteLine($"Total characters charged: {characterscharged}");
            return characterscharged;
        }

        /// <summary>
        /// Download a single blob item
        /// </summary>
        /// <param name="directory">Directory name to prepend to the file name.</param>
        /// <param name="blobItem">The actual blob</param>
        /// <returns>Task</returns>
        private async Task DownloadBlobAsync(DirectoryInfo directory, BlobItem blobItem, string tolanguage)
        {
            BlobClient blobClient = new(TranslationService.StorageConnectionString, TranslationService.ContainerClientTargets[tolanguage].Name, blobItem.Name);
            BlobDownloadInfo blobDownloadInfo = await blobClient.DownloadAsync();
            FileStream downloadFileStream;
            try
            {
                downloadFileStream = File.Create(directory.FullName + Path.DirectorySeparatorChar + blobItem.Name);
            }
            catch (IOException)
            {
                //This happens when target folder is same as source. Try again with a different file name in same folder.
                downloadFileStream = File.Create(directory.FullName + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(blobItem.Name) + "." + tolanguage + Path.GetExtension(blobItem.Name));
            }
            await blobDownloadInfo.Content.CopyToAsync(downloadFileStream);
            downloadFileStream.Close();
            logger.WriteLine("Downloaded: " + downloadFileStream.Name);
        }

        /// <summary>
        ///Delete older containers that may have been around from previous failed or abandoned runs
        /// </summary>
        /// <returns>Number of old containers that were deleted</returns>
        public async Task<int> ClearOldContainersAsync()
        {
            logger.WriteLine("START - Abandoned containers deletion.");
            int counter = 0;
            List<Task> deletionTasks = new();
            BlobServiceClient blobServiceClient = new(TranslationService.StorageConnectionString);
            var resultSegment = blobServiceClient.GetBlobContainersAsync(BlobContainerTraits.None, BlobContainerStates.None, "doctr").AsPages();
            await foreach (Azure.Page<BlobContainerItem> containerPage in resultSegment)
            {
                foreach (var containerItem in containerPage.Values)
                {
                    BlobContainerClient client = new(TranslationService.StorageConnectionString, containerItem.Name);
                    if (containerItem.Name.EndsWith("src")
                        || (containerItem.Name.Contains("tgt"))
                        || (containerItem.Name.Contains("gls")))
                    {
                        if (containerItem.Properties.LastModified < (DateTimeOffset.UtcNow - TimeSpan.FromDays(7)))
                        {
                            deletionTasks.Add(client.DeleteAsync());
                            counter++;
                        }
                    }
                }
            }
            await Task.WhenAll(deletionTasks);
            logger.WriteLine($"END - Abandoned containers deleted: {counter}");
            return counter;
        }

        /// <summary>
        /// Delete the containers created by this instance.
        /// </summary>
        /// <returns>The task only</returns>
        private async Task DeleteContainersAsync(string[] tolanguages)
        {
            logger.WriteLine("START - Container deletion.");
            List<Task> deletionTasks = new()
            {
                //delete the containers of this run
                TranslationService.ContainerClientSource.DeleteAsync()
            };
            foreach (string lang in tolanguages)
                deletionTasks.Add(TranslationService.ContainerClientTargets[lang].DeleteAsync());
            deletionTasks.Add(glossary.DeleteAsync());
            if (DateTime.Now.Millisecond < 100) deletionTasks.Add(ClearOldContainersAsync());  //Clear out old stuff ~ every 10th time. 
            await Task.WhenAll(deletionTasks);
            logger.WriteLine("END - Containers deleted.");
        }

        public static string Normalize(string filename)
        {
            return Path.GetFileName(filename);
        }

        /// <summary>
        /// Filters the list of files to the ones matching the extension.
        /// </summary>
        /// <param name="fileNames">List of files to filter.</param>
        /// <param name="validExtensions">Hash of valid extensions.</param>
        /// <param name="discarded">The files that were discarded</param>
        /// <returns>Tuple of the filtered list and the discards.</returns>
        public static (List<string>, List<string>) FilterByExtension(List<string> fileNames, HashSet<string> validExtensions)
        {
            if (fileNames is null) return (null, null);
            List<string> validNames = new();
            List<string> discardedNames = new();
            foreach (string filename in fileNames)
            {
                if (validExtensions.Contains(Path.GetExtension(filename).ToLowerInvariant())) validNames.Add(filename);
                else discardedNames.Add(filename);
            }
            return (validNames, discardedNames);
        }
    }
}

