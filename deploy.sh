#!/bin/bash

# Document Translation Web App Deployment Script
# This script deploys the Document Translation Web Application to Azure Container Apps

set -e

echo "🚀 Starting Document Translation Web App Deployment"
echo "=================================================="

# Check prerequisites
echo "🔍 Checking prerequisites..."

# Check Azure CLI
if ! command -v az &> /dev/null; then
    echo "❌ Azure CLI is not installed. Please install it first."
    echo "   Visit: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
fi

# Check Azure Developer CLI
if ! command -v azd &> /dev/null; then
    echo "❌ Azure Developer CLI is not installed. Please install it first."
    echo "   Visit: https://docs.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd"
    exit 1
fi

# Check Docker
if ! command -v docker &> /dev/null; then
    echo "❌ Docker is not available. Please ensure Docker is installed and running."
    echo "   For WSL2, enable WSL integration in Docker Desktop settings."
    exit 1
fi

# Verify Docker is running
if ! docker info &> /dev/null; then
    echo "❌ Docker daemon is not running. Please start Docker."
    exit 1
fi

echo "✅ All prerequisites are met!"

# Check if logged into Azure
echo "🔐 Checking Azure authentication..."
if ! az account show &> /dev/null; then
    echo "❌ Not logged into Azure. Please run 'az login' first."
    exit 1
fi

SUBSCRIPTION_ID=$(az account show --query id -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)
echo "✅ Authenticated to Azure (Subscription: $SUBSCRIPTION_ID)"

# Set environment variables if not set
if [ -z "$AZURE_ENV_NAME" ]; then
    echo "📝 Setting default environment name..."
    export AZURE_ENV_NAME="doctrans-$(date +%s | tail -c 5)"
    echo "   Environment: $AZURE_ENV_NAME"
fi

if [ -z "$AZURE_LOCATION" ]; then
    export AZURE_LOCATION="eastus"
    echo "📍 Using default location: $AZURE_LOCATION"
fi

echo ""
echo "🏗️  Deployment Configuration:"
echo "   Environment Name: $AZURE_ENV_NAME"
echo "   Location: $AZURE_LOCATION"
echo "   Subscription: $SUBSCRIPTION_ID"
echo ""

read -p "Continue with deployment? (y/N): " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "❌ Deployment cancelled."
    exit 1
fi

# Initialize azd if not already done
if [ ! -f ".azure/asconfig.json" ]; then
    echo "🔧 Initializing Azure Developer CLI..."
    azd init --environment $AZURE_ENV_NAME
fi

# Deploy the application
echo "🚀 Deploying application..."
azd up --environment $AZURE_ENV_NAME

# Get deployment outputs
echo "📤 Getting deployment outputs..."
RESOURCE_GROUP=$(azd env get-values | grep AZURE_RESOURCE_GROUP | cut -d'=' -f2 | tr -d '"')
TRANSLATOR_NAME=$(azd env get-values | grep AZURE_TRANSLATOR_NAME | cut -d'=' -f2 | tr -d '"')
STORAGE_NAME=$(azd env get-values | grep AZURE_STORAGE_ACCOUNT_NAME | cut -d'=' -f2 | tr -d '"')
KEYVAULT_NAME=$(azd env get-values | grep AZURE_KEY_VAULT_NAME | cut -d'=' -f2 | tr -d '"')
CONTAINER_APP_FQDN=$(azd env get-values | grep AZURE_CONTAINER_APP_FQDN | cut -d'=' -f2 | tr -d '"')

echo "🔑 Configuring secrets in Key Vault..."

# Get Translator key and store in Key Vault
echo "   Getting Translator service key..."
TRANSLATOR_KEY=$(az cognitiveservices account keys list \
    --name $TRANSLATOR_NAME \
    --resource-group $RESOURCE_GROUP \
    --query "key1" -o tsv)

az keyvault secret set \
    --vault-name $KEYVAULT_NAME \
    --name "TranslatorKey" \
    --value "$TRANSLATOR_KEY" \
    --output none

echo "   ✅ Translator key configured"

# Get Storage connection string and store in Key Vault
echo "   Getting Storage connection string..."
STORAGE_CONN_STRING=$(az storage account show-connection-string \
    --name $STORAGE_NAME \
    --resource-group $RESOURCE_GROUP \
    --query "connectionString" -o tsv)

az keyvault secret set \
    --vault-name $KEYVAULT_NAME \
    --name "StorageConnectionString" \
    --value "$STORAGE_CONN_STRING" \
    --output none

echo "   ✅ Storage connection string configured"

# Restart container app to pick up new secrets
echo "🔄 Restarting container app to load secrets..."
CONTAINER_APP_NAME=$(azd env get-values | grep AZURE_CONTAINER_APP_NAME | cut -d'=' -f2 | tr -d '"')
az containerapp revision restart \
    --name $CONTAINER_APP_NAME \
    --resource-group $RESOURCE_GROUP \
    --output none

echo ""
echo "🎉 Deployment completed successfully!"
echo "=================================================="
echo ""
echo "📋 Deployment Summary:"
echo "   Environment: $AZURE_ENV_NAME"
echo "   Resource Group: $RESOURCE_GROUP"
echo "   Application URL: https://$CONTAINER_APP_FQDN"
echo ""
echo "🔗 Access your application:"
echo "   Web App: https://$CONTAINER_APP_FQDN"
echo "   API Docs: https://$CONTAINER_APP_FQDN/swagger"
echo "   Health Check: https://$CONTAINER_APP_FQDN/health"
echo ""
echo "🛠️  Manage your deployment:"
echo "   View logs: azd logs"
echo "   Monitor: Visit Azure Portal → Container Apps → $CONTAINER_APP_NAME"
echo "   Redeploy: azd deploy"
echo "   Clean up: azd down"
echo ""
echo "✨ Happy translating!"
