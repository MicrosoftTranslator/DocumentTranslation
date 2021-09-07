# Microsoft Document Translation

Translate local files or network files in many different formats, to more than 90 different languages.
Supported formats include HTML, PDF, all Office document formats, Markdown and plain text. 
The complete [list of document formats is here](https://docs.microsoft.com/azure/cognitive-services/translator/document-translation/overview#supported-document-formats).

You can select up to 1000 files and translate them to a different language with a single command.
The Windows UI gives you options to comfortably select source files, target language, and the folder you want to deposit the translations in.
It comes with a command line utility that does the same thing using a command line interface. 
Document Translation uses the Azure Translator Service to perform the translations. You need a subscription to Azure, and register
a Translator resource as well as a storage resource. [The documentation](https://microsofttranslator.github.io/DocumentTranslation) gives detailed instructions on how to obtain those. 

For the translation you can specify a glossary (custom dictionary) to use. You can also make use of a custom translation system
you may have built with [Custom Translator](http://customtranslator.ai).

**Main UI**

![Main UI](docs/images/Running.png)

A simple copy-and-paste text translation interface is present in the Windows UI. 

**Text Translation UI**

![Text Translate](docs/images/TextTranslate.png)

## Download

Signed binaries are provided in the [releases folder](https://github.com/microsofttranslator/documenttranslation/releases).

## Documentation

See the [complete documentation of the tool](https://microsofttranslator.github.io/DocumentTranslation).

The documentation is stored in the /docs folder of the project. 

## Implementation

Document Translation is written and compiled for .Net 5. The command line utility should be compatible with other platforms
running .Net 5, namely MacOS and Linux. Tested only on Windows 10 at this point. Please let us know via an issue if you find problems with
other platforms running .Net 5. 
Signed binaries are provided in the [releases](https://github.com/microsofttranslator/documenttranslation/releases) folder.
To compile yourself, run Visual Studio 2019 and have the .Net 5 SDK installed.

This tool makes use of the Azure Document Translation service. The Azure Document Translation service translates
a set of documents that reside in an Azure storage container, and delivers the translations in another Azure storage
container. This tool provides a local interface to that service, allowing you to translate a locally rediding file
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

Future optimization includes a shared storage for the glossary, so that multiple clients can refer to a single company-wide glossary. 

## Issues

Please submit an issue for questions or comments, and of course for any bug or problem you encouter
[here](https://github.com/MicrosoftTranslator/DocumentTranslation/issues).

## Contributions
Please contribute your bug fix and functionality additions. Submit a pull request. We will review and integrate
quickly - or reject with comments.

## Future plans

- Option to extend the set of file formats with format conversions that are processed locally, as a library within this tool.
- Authentication with Azure Actove Directory
- Upgrade to .Net 6 when it becomes generally available. 


## Credits
The tool uses following Nuget packages:
- Nate McMaster's Command Line Utilities for the CLI command and options processing. 
- Azure.Storage.Blobs for the interaction with the Azure storage service. 
- Azure.AI.Translation.Document, a client library for the Azure Document Translation Service
- Azure.Identity for authentication to Key Vault
- Azure.Security.KeyVault.Secrets for reading the credentials from Azure Key Vault

Our sincere thanks to the authors of these packages.
