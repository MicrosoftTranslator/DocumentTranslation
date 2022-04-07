del bin\release\DocumentTranslation.zip
md temp
del temp\*.* /s /f /q

xcopy /q ..\DocumentTranslation.GUI\bin\Release\net6.0-windows\*.* temp
xcopy /q /y ..\DocumentTranslation.CLI\bin\Release\net6.0\*.* temp

tar.exe -a -c -p -f bin\release\DocumentTranslation.zip -C temp *.*

del temp\*.* /s /f /q
rd temp