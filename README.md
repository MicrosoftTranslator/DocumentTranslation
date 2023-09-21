# Microsoft Document Translation

Translate local files or network files in many different formats, to more than 100 different languages.
Supported formats include HTML, PDF, all Office document formats, Markdown, MHTML, Outlook .MSG, XLIFF, CSV, TSV and plain text. 
The complete [list of document formats is here](https://docs.microsoft.com/azure/cognitive-services/translator/document-translation/overview#supported-document-formats).

You can select up to 1000 files and translate them to one or more different languages with a single command.
The Windows UI gives you options to comfortably select source files, one or more target languages, and the folder you want to deposit the translations in.
It comes with a command line utility that does the same thing using a command line interface. 
Document Translation uses the Azure Translator Service to perform the translations. You need a subscription to Azure, and register
a Translator resource as well as a storage resource. [The documentation](https://microsofttranslator.github.io/DocumentTranslation) gives
detailed instructions on how to obtain those. 

For the translation you can specify a glossary (custom dictionary) to use. You can also make use of a custom translation system
you may have built with [Custom Translator](http://customtranslator.ai).

You can manage the credentials for accessing the Azure services in Azure Key Vault - the app will read it from there,
based on your identity. Good if you want to manage the credentials centrally.

Works with Azure sovereign clouds. 

**Document Translation  UI**

The main UI provides document translation: Multiple documents to multiple languages.

![Main UI](docs/images/Running.png)


**Text Translation UI**

A simple copy-and-paste text translation interface is present in the Windows UI. 

![Text Translate](docs/images/TextTranslate.png)

## Download

A Windows installer (.MSI) and signed binaries (.ZIP) for manual installation on other OSes are provided in
the [releases folder](https://github.com/microsofttranslator/documenttranslation/releases).

## Documentation

See the [complete documentation of the tool](https://microsofttranslator.github.io/DocumentTranslation).

The documentation is stored in the /docs folder of the project. 

## Implementation

Document Translation is written and compiled for .Net 6. The command line utility should be compatible with other platforms
running .Net 6, namely MacOS and Linux. Tested on Windows 10, Windows 11 and Mac OS X at this point. Please let us know via an issue
if you find problems with other platforms running .Net 6. 
Signed binaries are provided in the [releases](https://github.com/microsofttranslator/documenttranslation/releases) folder.
To compile yourself, run Visual Studio 2022 and have the .Net 6 SDK installed.
You can compile and run the tool in Visual Studio 2022.

This tool makes use of the Azure Document Translation service. The Azure Document Translation service translates
a set of documents that reside in an Azure storage container, and delivers the translations in another Azure storage
container. This app provides a local interface to that service, allowing you to translate a locally residing file
or a folder, and receiving the translation of these documents in a local folder.
The tool uploads the local documents, invokes the translation, monitors the translation progress,
downloads the translated documents to your local machine, and then deletes the containers from the service.
Each run is independent of each other by giving the containers it uses a unique name within the common storage account.
Multiple people may run translations concurrently, using the same credentials and the same storage account.

Project "doctr" contains the command line processing based on Nate McMaster's Command Line Utilities. All user interaction
is handled here.
Project 'DocumentTranslationService' contains three relevant classes: DocumentTranslationService handles all the interaction
with the Azure service.
DocumentTranslationBusiness handles the local file operations and business logic.
Class 'Glossary' handles the upload of the glossary, when a glossary is specified.

Works with Azure sovereign clouds. The app accepts fully qualified service endpoints.

## Privacy and Security

This client side app is a lightweight frontend to the Azure Document Translation service. 
The Azure Document Translation service uses Azure Blob Storage to read the documents to be translated from and it 
deposits the translated documents into Azure Blob Storage. All processing and storage is within the user-provided
accounts. This app does not have its own Azure credentials; user supplies the identities and authentication for Azure services.
In a successful run, the app uploads the user-suplied local documents to Azure Blob Storage in user's Azure account,
initiates the translation, waits for completion, downloads the translated documents to the user-specified location,
and then deletes all original and translated copies of the documents from Azure Blob Storage.
In an unsuccessful run the deletion may be skipped, leaving abandoned storage containers behind. On average every 10th run of the app 
will automatically delete any left-behind storage containers that are older than one week. 
The command line command `doctr clear` forces a deletion of storage containers older than one week. For a faster deletion of
storage containers, user will have to perform the deletion manually within the Azure storage account. A faster deletion has the
chance to disrupt the translation runs of other users using the same Azure credentials.
 
The Azure privacy statement applies.

The app stores the Azure credentials in a settings file in JSON format, unencrypted, in the user's app settings folder:
`C:\Users\<user>\AppData\Roaming\Document Translation` as `appsettings.json` on Windows, and in the `/usr/` folder on MacOS
and other Unix flavors.  
To avoid storing any Azure credentials on the client, please use the Azure Key Vault. In this case only the URL to the customer's
Key Vault is stored in the user's settings file. Other Azure credentials are stored in the user's key vault. See the Key Vault section
in the Document Translator documentation.
The app stores UI settings (not credentials) in the `uisettings.json` file in the user settings folder. It stores a log of the last
run in `docTrLog.txt` in the same folder. It stores references to custom translation systems in `CustomCategories.json`,
also in the same folder.

The app does not create any local copies of the original or translated documents, not even temporary,
EXCEPT in the case of document formats that are locally supplied. As of September 2023 the locally supplied formats
are SubRIP (SRT) and WebVTT (VTT) formats. The app will create a temporary file in the user's temp folder, storing
the content of the file to be translated in MarkDown format. It will create a temporary file in the MarkDown format in
the target folder for translated documents. A successful run of the translation will delete the temporary files. 
While converting between the local format and MarkDown, the app processes the content of the file in memory. 

The app does not include any telemetry or instrumentation. It does not report usage to any service. 


## Issues

Please submit an issue for questions or comments, and of course for any bug or problem you encouter
[here](https://github.com/MicrosoftTranslator/DocumentTranslation/issues).

## Contributions
Please contribute your bug fix and functionality additions. Submit a pull request. We will review and integrate
quickly - or reject with comments.

## Future plans

- Option to extend the set of file formats with format conversions that are processed locally, as a library within this tool.
- Web interface with .Net 6 MAUI
- A shared storage for the glossary, so that multiple clients can refer to a
single company-wide glossary. 


## Credits
The tool uses following Nuget packages:
- Nate McMaster's Command Line Utilities for the CLI command and options processing. 
- Azure.Storage.Blobs for the interaction with the Azure storage service. 
- Azure.AI.Translation.Document, a client library for the Azure Document Translation Service
- Azure.Identity for authentication to Key Vault
- Azure.Security.KeyVault.Secrets for reading the credentials from Azure Key Vault

Our sincere thanks to the authors of these packages.
