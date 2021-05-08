# DOCTR: Microsoft Document Translation Command Line Interface (CLI)
The Microsoft Document Translation Command Line Interface gives quick access to document translation functions.
It is a simple program which makes use of the server-side document translation functionality, giving it a client-based
command line interface, and allowing you translate local documents, in any of the the supported file formats. Use 'doctr formats'
to list the available formats.

## Download
Pleae download the latest binary from the "Releases" section.
## Quickstart
## Overview

## Minimum requirements
- An Azure subscription
- A Translator resource in your Azure subscription
- A Blob storage resource in your Azure subscription
- A Windows computer able to run this executable. The code is written in .Net 5.0 and able to compile and run on other platforms that
.Net 5.0 is present on, but the executable for other platforms is currently not provided here.

## Usage
### Configure the tool
The configuration contains the credentials for the needed Azure resources:
The minimum needed credentials are
- The subscription key to the Translator resource.
- The name of the Translator resource 
- A storage connection string.
You can obtain all of these from the Azure portal.

'doctr config --set storage <Storage Connection String>	| Required
'doctr config --set key <Subscription key of the Translator resource>	| Required
'doctr config --set name <Name of the Azure Translator resource>	| Required
'doctr config --set category <Custom Translator category ID>	| Optional
The configuration settings are stored in the file appsettings.json, in the current folder.
You may edit the file by hand, using the editor of your choice. 

You can inspect the settings using the following commands:
'doctr config list'	| List the current configuration settings.
'doctr config test'	| Validate the credentials and report which one is failing.

### List capabilities
'doctr languages'	| List the available languages. Can be listed before credentials are set.
'doctr formats'		| List the file formats available for translation. Requires credentials key, name and storage to be set.
'doctr glossary'		| List the glossary formats available for use as glossary. Requires credentials key, name and storage to be set.

### Translate
'doctr translate'

## Implementation Details
## Contributions
Please contribute your bug fix, and functionality additions. Submit a pull request. We will review and integrate
quickly - or reject with comments.