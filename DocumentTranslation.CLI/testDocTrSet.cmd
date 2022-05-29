REM fill all the <elements> with the values from your Azure portal
cd bin\debug\net6.0
doctr config set --key <myResourceKey>
doctr config set --storage <my Storage Connection String (starts with "DefaultEndpointsProtocol=")
doctr config set --endpoint https://<something>.cognitiveservices.azure.com/
doctr config set --region eastus2
doctr config set --category sometestcategory
pause
doctr config list
pause
doctr config test
cd ..\..\..