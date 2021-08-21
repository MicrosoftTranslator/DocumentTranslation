## To sign the binaries for this project, have a signing certificate installed on the machine you build on
## Run this script once afer you build the executables by opning the solution folder in Explorer
## and then executing this script by right-click "Run with Powershell"
## Run this again after building the installer to sign the .msi

## Adjust the path to your installation of the Windows SDK.
$signtoolLocation = '"C:\Program Files (x86)\Microsoft Visual Studio\Shared\NuGetPackages\microsoft.windows.sdk.buildtools\10.0.19041.8\bin\10.0.19041.0\x64\signtool.exe" '
$signtoolCommand = 'sign /a '


## Add any files you need to sign here. Do not re-sign the Nuget package files.
$files =
"DocumentTranslation.GUI\bin\Release\net5.0-windows\DocumentTranslation.GUI.dll",
"DocumentTranslation.GUI\bin\Release\net5.0-windows\DocumentTranslation.GUI.exe",
"DocumentTranslationService.Core\bin\Release\net5.0\DocumentTranslationService.dll",
"DocumentTranslation.CLI\bin\Release\net5.0\doctr.exe",
"DocumentTranslation.CLI\bin\Release\net5.0\doctr.dll",
"DocumentTranslation.Setup\bin\Release\DocumentTranslation.Setup.msi"

foreach ($file in $files)
{
	&cmd.exe /c  (-join ("$signtoollocation", "$signtoolCommand", "$file"))
}

## Just wait to show you success or failure of the signing command
$Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
