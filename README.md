#Microsoft Document Translation Command Line Interface (CLI)
The Microsoft Document Translation Command Line Interface gives quick access to document translation functions.
It is a simple program which makes use of the server-side document translation functionality, giving it a client-based
command line interface.
##Download
Pleae download the latest binary from the "Releases" section.
##Quickstart
##Overview
##Minimum requirements
- An Azure subscription
- A Translator resource in your Azure subscription
- A Blob storage resource in your Azure subscription
- A Windows computer able to run this executable. The code is written in .Net 5.0 and able to compile and run on other platforms that
.Net 5.0 is present on, but the executable for other platforms is currently not provided here.

##Usage
###Configuration
The configuration contains the credentials for the needed Azure resources:
The minimum needed credentials are
- The subscription key to the Translator resource.
- The the 
Create an appsettings.json in the current folder. You can modify the content of the appsettings.json using the editor of
your choice, or you can use
'doctr config --set storage <Storage Connection String>	|
'doctr config --set key <Subscription key of the Translator resource>	|
'doctr config --set name <Name of the Azure Translator resource>	| 
'doctr config list'	| List the current configuration settings.
'doctr config test'	| Validate the credentials and report which one is failing.
'doctr languages'	| List the available languages. Can be done before credentials are set.
'doctr formats'		| List the file formats available for translation
'doctr test <folder>' 

##Implementation Details
##Contributions
Please controbute your bug fix, and functionality additions. Submit a pull request. We will review and integrate
quickly - or reject with comments.