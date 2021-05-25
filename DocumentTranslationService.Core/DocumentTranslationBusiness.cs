using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace DocumentTranslationService.Core
{
    public class DocumentTranslationBusiness
    {
        #region Properties
        public DocumentTranslationService TranslationService { get; }

        /// <summary>
        /// Can retrieve the final target folder here
        /// </summary>
        public string TargetFolder { get; private set; }
        /// <summary>
        /// Returns the files used as glossary.
        /// </summary>
        public Glossary Glossary { get; private set; }

        /// <summary>
        /// Prevent deletion of storage container. For debugging.
        /// </summary>
        public bool Nodelete { get; set; } = false;

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
        /// Fires when the source files completed uploading.  
        /// Returns count and total size of the download.
        /// </summary>
        public event EventHandler<(int, long)> OnUploadComplete;

        /// <summary>
        /// Fires if there were files listed to translate that were discarded.
        /// </summary>
        public event EventHandler<List<string>> OnFilesDiscarded;

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
        /// <param name="tolanguage">A single target language</param>
        /// <param name="glossaryfiles">The glossary files</param>
        /// <returns></returns>
        public async Task RunAsync(List<string> filestotranslate, string tolanguage, List<string> glossaryfiles = null, string targetFolder = null)
        {
            if (filestotranslate.Count == 0) throw new ArgumentNullException(nameof(filestotranslate), "No files to translate.");
            Task initialize = TranslationService.InitializeAsync();

            #region Build the list of files to translate
            List<string> sourcefiles = new();
            foreach (string filename in filestotranslate)
            {
                if (File.GetAttributes(filename) == FileAttributes.Directory)
                    foreach (var file in Directory.EnumerateFiles(filename))
                        sourcefiles.Add(file);
                else sourcefiles.Add(filename);
            }
            List<string> discards;
            await initialize;
            #endregion

            #region Parameter checking
            if (TranslationService.Extensions.Count == 0)
                throw new ArgumentNullException(nameof(TranslationService.Extensions), "List of translatable extensions cannot be null.");
            (sourcefiles, discards) = FilterByExtension(sourcefiles, TranslationService.Extensions);
            if (discards is not null)
            {
                foreach (string fileName in discards)
                {
                    Debug.WriteLine($"Run: Discarded due to invalid file format for translation: {fileName}");
                }
                if ((OnFilesDiscarded is not null) && (discards.Count > 0)) OnFilesDiscarded(this, discards);
            }
            if (sourcefiles.Count == 0)
            {
                //There is nothing to translate
                Debug.WriteLine("Run: Nothing left to translate.");
                throw new ArgumentNullException(nameof(filestotranslate), "List filtered to nothing.");
            }
            if (!TranslationService.Languages.ContainsKey(tolanguage)) throw new ArgumentException("Invalid 'to' language.", nameof(tolanguage));
            #endregion

            #region Create the containers
            string containerNameBase = "doctr" + Guid.NewGuid().ToString();

            BlobContainerClient sourceContainer = new(TranslationService.StorageConnectionString, containerNameBase + "src");
            var sourceContainerTask = sourceContainer.CreateIfNotExistsAsync();
            TranslationService.ContainerClientSource = sourceContainer;
            BlobContainerClient targetContainer = new(TranslationService.StorageConnectionString, containerNameBase + "tgt");
            var targetContainerTask = targetContainer.CreateIfNotExistsAsync();
            TranslationService.ContainerClientTarget = targetContainer;
            Glossary glossary = new(TranslationService, glossaryfiles);
            this.Glossary = glossary;
            #endregion

            #region Upload documents
            await sourceContainerTask;
            Debug.WriteLine("Source container created");
            int count = 0;
            long sizeInBytes = 0;
            List<Task> uploadTasks = new();
            using (System.Threading.SemaphoreSlim semaphore = new(100))
            {
                foreach (var filename in sourcefiles)
                {
                    await semaphore.WaitAsync();
                    FileStream fileStream = File.OpenRead(filename);
                    BlobClient blobClient = new(TranslationService.StorageConnectionString, TranslationService.ContainerClientSource.Name, Normalize(filename));
                    try
                    {
                        uploadTasks.Add(blobClient.UploadAsync(fileStream, true));
                        count++;
                        sizeInBytes += new FileInfo(fileStream.Name).Length;
                        semaphore.Release();
                    }
                    catch (System.AggregateException e)
                    {
                        Debug.WriteLine($"Uploading file {fileStream.Name} failed with {e.Message}");
                    }
                    Debug.WriteLine($"File {filename} uploaded.");
                }
            }
            Debug.WriteLine("Awaiting upload task completion.");
            await Task.WhenAll(uploadTasks);
            //Upload Glossaries
            var result = await glossary.UploadAsync(TranslationService.StorageConnectionString, containerNameBase);
            if (OnUploadComplete is not null) OnUploadComplete(this, (count, sizeInBytes));
            #endregion

            #region Translate the container content
            Uri sasUriSource = sourceContainer.GenerateSasUri(BlobContainerSasPermissions.All, DateTimeOffset.UtcNow + TimeSpan.FromHours(5));
            await targetContainerTask;
            Uri sasUriTarget = targetContainer.GenerateSasUri(BlobContainerSasPermissions.All, DateTimeOffset.UtcNow + TimeSpan.FromHours(5));
            DocumentTranslationSource documentTranslationSource = new() { SourceUrl = sasUriSource.ToString() };
            DocumentTranslationTarget documentTranslationTarget = new(language: tolanguage, targetUrl: sasUriTarget.ToString());
            List<ServiceGlossary> serviceGlossaries = new();
            if (glossary.Glossaries is not null)
                foreach (var glos in glossary.Glossaries)
                    serviceGlossaries.Add(glos.Value);
            documentTranslationTarget.glossaries = serviceGlossaries.ToArray();
            if (TranslationService.Category is not null)
            {
                documentTranslationTarget.category = TranslationService.Category;
            }

            List<DocumentTranslationTarget> documentTranslationTargets = new() { documentTranslationTarget };

            DocumentTranslationInput input = new() { storageType = "folder", source = documentTranslationSource, targets = documentTranslationTargets };
            try
            {
                TranslationService.ProcessingLocation = await TranslationService.SubmitTranslationRequestAsync(input);
            }
            catch (ServiceErrorException ex)
            {
                OnStatusUpdate?.Invoke(this, TranslationService.errorResponse);
            }
            Debug.WriteLine("Processing-Location: " + TranslationService.ProcessingLocation);
            if (TranslationService.ProcessingLocation is null)
            {
                Debug.WriteLine("ERROR: Start of translation job failed.");
                return;
            }

            //Check on status until status is in a final state
            StatusResponse statusResult;
            string lastActionTime = string.Empty;
            do
            {
                await Task.Delay(1000);
                statusResult = await TranslationService.CheckStatusAsync();
                if (statusResult.lastActionDateTimeUtc != lastActionTime)
                {
                    //Raise the update event
                    if (OnStatusUpdate is not null) OnStatusUpdate(this, statusResult);
                    lastActionTime = statusResult.lastActionDateTimeUtc;
                }
            }
            while (
                  (statusResult.summary.inProgress != 0)
                || (statusResult.status == "NotStarted")
                || (statusResult.summary.notYetStarted != 0)
                || (statusResult.status == "Canceled"));
            if (OnStatusUpdate is not null) OnStatusUpdate(this, statusResult);
            if (statusResult.status.Contains("Failed.")) return;
            #endregion

            #region Download the translations
            //Chance for optimization: Check status on the documents and start download immediately after each document is translated. 
            string directoryName;
            if (string.IsNullOrEmpty(targetFolder)) directoryName = Path.GetDirectoryName(sourcefiles[0]) + "." + tolanguage;
            else directoryName = targetFolder;
            count = 0;
            sizeInBytes = 0;
            DirectoryInfo directory = Directory.CreateDirectory(directoryName);
            List<Task> downloads = new();
            using (System.Threading.SemaphoreSlim semaphore = new(100))
            {
                await foreach (var blobItem in TranslationService.ContainerClientTarget.GetBlobsAsync())
                {
                    await semaphore.WaitAsync();
                    downloads.Add(DownloadBlobAsync(directory, blobItem));
                    count++;
                    sizeInBytes += (long)blobItem.Properties.ContentLength;
                    semaphore.Release();
                }
            }
            await Task.WhenAll(downloads);
            #endregion
            this.TargetFolder = directoryName;
            #region final
            Debug.WriteLine("Download complete.");
            if (OnDownloadComplete is not null) OnDownloadComplete(this, (count, sizeInBytes));
            if (!Nodelete) await DeleteContainersAsync();
            Debug.WriteLine("Run: Exiting.");
            #endregion
        }

        /// <summary>
        /// Download a single blob item
        /// </summary>
        /// <param name="directory">Directory name to prepend to the file name.</param>
        /// <param name="blobItem">The actual blob</param>
        /// <returns>Task</returns>
        private async Task DownloadBlobAsync(DirectoryInfo directory, BlobItem blobItem)
        {
            BlobClient blobClient = new(TranslationService.StorageConnectionString, TranslationService.ContainerClientTarget.Name, blobItem.Name);
            BlobDownloadInfo blobDownloadInfo = await blobClient.DownloadAsync();
            FileStream downloadFileStream = File.Create(directory.FullName + Path.DirectorySeparatorChar + blobItem.Name);
            await blobDownloadInfo.Content.CopyToAsync(downloadFileStream);
            downloadFileStream.Close();
            Debug.WriteLine("Downloaded: " + downloadFileStream.Name);
        }

        /// <summary>
        ///Delete older containers that may have been around from previous failed or abandoned runs
        /// </summary>
        /// <returns>Number of old containers that were deleted</returns>
        public async Task<int> ClearOldContainersAsync()
        {
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
                        || (containerItem.Name.EndsWith("tgt"))
                        || (containerItem.Name.EndsWith("gls")))
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
            Debug.WriteLine("Old Containers deleted.");
            return counter;
        }

        /// <summary>
        /// Delete the containers created by this instance.
        /// </summary>
        /// <param name="CleanUpAll">Optional: If set, delete all containers following the naming scheme that have been last accessed more than 10 days ago.</param>
        /// <returns>The task only</returns>
        private async Task DeleteContainersAsync()
        {
            List<Task> deletionTasks = new();
            //delete the containers of this run
            deletionTasks.Add(TranslationService.ContainerClientSource.DeleteAsync());
            deletionTasks.Add(TranslationService.ContainerClientTarget.DeleteAsync());
            deletionTasks.Add(Glossary.DeleteAsync());
            if (DateTime.Now.Millisecond < 100) deletionTasks.Add(ClearOldContainersAsync());  //Clear out old stuff ~ every 10th time. 
            await Task.WhenAll(deletionTasks);
            Debug.WriteLine("Containers deleted.");
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

