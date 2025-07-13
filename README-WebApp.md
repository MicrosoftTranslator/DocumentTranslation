# Document Translation Web Application

A modern web application for translating documents and text using Azure Translator Service. This application can be deployed to Azure Container Apps.

## Features

- **Text Translation**: Translate text in real-time between supported languages
- **Document Translation**: Upload and translate documents (PDF, DOCX, XLSX, PPTX, TXT, HTML, RTF, ODT, ODS, ODP)
- **Batch Translation**: Process multiple documents at once
- **Modern Web UI**: Clean, responsive interface built with Bootstrap 5
- **Real-time Progress**: Track translation progress for batch operations
- **Secure**: Uses Azure Managed Identity for secure access to Azure services

## Architecture

The application consists of:

- **ASP.NET Core Web API**: Backend service providing translation APIs
- **Static Web Frontend**: Modern HTML/CSS/JavaScript frontend
- **Azure Container Apps**: Hosting platform with auto-scaling
- **Azure Translator**: AI-powered translation service
- **Azure Storage**: Document storage and processing
- **Azure Key Vault**: Secure secrets management
- **Azure Container Registry**: Container image storage

## Prerequisites

Before deploying this application, ensure you have:

1. **Azure CLI** installed (`az --version`)
2. **Azure Developer CLI** installed (`azd version`)
3. **Docker** installed and running (`docker version`)
4. An **Azure subscription** with appropriate permissions

## Quick Deployment

### 1. Clone and Navigate
```bash
cd DocumentTranslation
```

### 2. Initialize Azure Developer CLI
```bash
azd init
```

### 3. Deploy to Azure
```bash
azd up
```

This will:
- Create all necessary Azure resources
- Build and push the container image
- Deploy the application to Azure Container Apps
- Configure all security and networking

### 4. Post-Deployment Setup
After deployment, you'll need to configure the secrets in Key Vault:

```bash
# Get the Translator key and store it in Key Vault
TRANSLATOR_KEY=$(az cognitiveservices account keys list --name $AZURE_TRANSLATOR_NAME --resource-group $AZURE_RESOURCE_GROUP --query "key1" -o tsv)
az keyvault secret set --vault-name $AZURE_KEY_VAULT_NAME --name "TranslatorKey" --value "$TRANSLATOR_KEY"

# Get the Storage connection string and store it in Key Vault
STORAGE_CONN_STRING=$(az storage account show-connection-string --name $AZURE_STORAGE_ACCOUNT_NAME --resource-group $AZURE_RESOURCE_GROUP --query "connectionString" -o tsv)
az keyvault secret set --vault-name $AZURE_KEY_VAULT_NAME --name "StorageConnectionString" --value "$STORAGE_CONN_STRING"
```

## Manual Deployment

If you prefer to deploy manually or need more control:

### 1. Create Resource Group
```bash
az group create --name rg-document-translation --location eastus
```

### 2. Deploy Infrastructure
```bash
az deployment group create \
  --resource-group rg-document-translation \
  --template-file infra/main.bicep \
  --parameters environmentName=myapp location=eastus
```

### 3. Build and Push Container
```bash
# Build the container image
docker build -t document-translation-web .

# Tag and push to Azure Container Registry
az acr login --name <your-registry-name>
docker tag document-translation-web <your-registry-name>.azurecr.io/document-translation-web:latest
docker push <your-registry-name>.azurecr.io/document-translation-web:latest
```

### 4. Update Container App
```bash
az containerapp update \
  --name <your-container-app-name> \
  --resource-group rg-document-translation \
  --image <your-registry-name>.azurecr.io/document-translation-web:latest
```

## Configuration

The application uses the following environment variables:

| Variable | Description | Required |
|----------|-------------|----------|
| `DocumentTranslation__AzureResourceName` | Azure Translator endpoint | Yes |
| `DocumentTranslation__SubscriptionKey` | Azure Translator API key | Yes |
| `DocumentTranslation__AzureRegion` | Azure region | Yes |
| `DocumentTranslation__StorageConnectionString` | Azure Storage connection string | Yes |
| `DocumentTranslation__AzureKeyVaultName` | Azure Key Vault name | Yes |
| `DocumentTranslation__TextTransEndpoint` | Text translation endpoint | No |
| `DocumentTranslation__ShowExperimental` | Show experimental languages | No |
| `DocumentTranslation__Category` | Custom Translator category | No |
| `DocumentTranslation__FlightString` | Experimental flights | No |

## Local Development

### 1. Configure User Secrets
```bash
cd DocumentTranslation.Web
dotnet user-secrets set "DocumentTranslation:AzureResourceName" "your-translator-resource"
dotnet user-secrets set "DocumentTranslation:SubscriptionKey" "your-translator-key"
dotnet user-secrets set "DocumentTranslation:StorageConnectionString" "your-storage-connection-string"
# ... other settings
```

### 2. Run the Application
```bash
dotnet run
```

The application will be available at `https://localhost:5001` or `http://localhost:5000`.

## Usage

### Text Translation
1. Navigate to the "Text Translation" tab
2. Select source and target languages
3. Enter text to translate
4. Click "Translate Text"

### Document Translation
1. Navigate to the "Document Translation" tab
2. Select source and target languages
3. Choose a document file
4. Click "Translate Document"
5. Download the translated document

### Batch Translation
1. Navigate to the "Batch Translation" tab
2. Select source and target languages
3. Choose multiple document files
4. Click "Start Batch Translation"
5. Monitor progress and download results

## Supported File Formats

- **Documents**: PDF, DOCX, XLSX, PPTX, ODT, ODS, ODP
- **Text Files**: TXT, HTML, RTF
- **Maximum Size**: 50MB per file

## Troubleshooting

### Common Issues

1. **Docker not available**: Ensure Docker Desktop is running and WSL integration is enabled
2. **Authentication errors**: Verify Azure CLI login with `az account show`
3. **Permission errors**: Ensure your account has Contributor access to the subscription
4. **Translation errors**: Check Azure Translator service quotas and limits

### Logs and Monitoring

View application logs:
```bash
azd logs
```

Or check in Azure Portal:
- Container Apps → Your App → Log stream
- Application Insights → Live metrics

## Security

The application follows Azure security best practices:

- **Managed Identity**: No credentials stored in code
- **Key Vault**: Secure secret storage
- **Private Networking**: Optional VNET integration
- **HTTPS**: Enforced for all communications
- **RBAC**: Principle of least privilege

## Cost Optimization

- Container Apps scale to zero when not in use
- Azure Translator charges per character translated
- Storage costs are minimal for typical usage
- Consider Azure Translator free tier for development

## Support

For issues and questions:
1. Check the [Azure Translator documentation](https://docs.microsoft.com/azure/cognitive-services/translator/)
2. Review [Container Apps documentation](https://docs.microsoft.com/azure/container-apps/)
3. File issues in the project repository

## License

This project is licensed under the MIT License - see the LICENSE file for details.
