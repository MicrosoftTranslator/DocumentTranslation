#!/bin/bash

# Debug Docker Setup Script for Document Translation Web Application
# This script sets up Docker debugging with VS Code

set -e  # Exit on any error

# Configuration
IMAGE_NAME="document-translation-web"
DEBUG_TAG="debug"
CONTAINER_NAME="document-translation-debug"
DEBUG_PORT="5000"

echo "🐛 Setting up Docker debugging for Document Translation Web Application"
echo "======================================================================"

# Ensure we're in the right directory
if [ ! -f "Dockerfile.debug" ]; then
    echo "❌ Error: Dockerfile.debug not found. Make sure you're in the project root directory."
    exit 1
fi

if [ ! -f "DocumentTranslation.Web/DocumentTranslation.Web.csproj" ]; then
    echo "❌ Error: Web project not found. Make sure you're in the project root directory."
    exit 1
fi

echo "📋 Debug Configuration:"
echo "   - Image Name: ${IMAGE_NAME}:${DEBUG_TAG}"
echo "   - Container Name: ${CONTAINER_NAME}"
echo "   - Debug Port: ${DEBUG_PORT}"
echo "   - .NET Version: 9.0"
echo ""

# Stop and remove existing debug container if it exists
if docker ps -a --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
    echo "🛑 Stopping existing debug container..."
    docker stop "$CONTAINER_NAME" || true
    echo "🗑️  Removing existing debug container..."
    docker rm "$CONTAINER_NAME" || true
fi

# Build the debug Docker image
echo "🔨 Building debug Docker image..."
docker build \
    --file Dockerfile.debug \
    --build-arg BUILD_CONFIGURATION=Debug \
    --tag "${IMAGE_NAME}:${DEBUG_TAG}" \
    --progress=plain \
    .

if [ $? -eq 0 ]; then
    echo ""
    echo "✅ Debug Docker image built successfully!"
    echo ""
    
    # Show image information
    echo "📊 Debug Image Information:"
    docker images "${IMAGE_NAME}" --filter "reference=${IMAGE_NAME}:${DEBUG_TAG}" --format "table {{.Repository}}\t{{.Tag}}\t{{.Size}}\t{{.CreatedAt}}"
    echo ""
    
    echo "🚀 Debug container is ready!"
    echo ""
    echo "🔧 To debug in VS Code:"
    echo "   1. Open VS Code in this workspace"
    echo "   2. Press F5 or go to Run and Debug"
    echo "   3. Select 'Docker .NET Launch' configuration"
    echo "   4. Set breakpoints in your code"
    echo "   5. Start debugging!"
    echo ""
    echo "🌐 Manual debug run:"
    echo "   docker run -d -p ${DEBUG_PORT}:80 --name ${CONTAINER_NAME} ${IMAGE_NAME}:${DEBUG_TAG}"
    echo ""
    echo "🔍 Debug endpoints:"
    echo "   - Application: http://localhost:${DEBUG_PORT}"
    echo "   - Health Check: http://localhost:${DEBUG_PORT}/health"
    echo "   - Swagger UI: http://localhost:${DEBUG_PORT}/swagger"
    echo ""
    echo "📋 VS Code debugging features:"
    echo "   - Set breakpoints in C# code"
    echo "   - Step through code execution"
    echo "   - Inspect variables and call stack"
    echo "   - Hot reload for code changes"
    echo "   - Debug console output"
    
else
    echo ""
    echo "❌ Debug Docker build failed!"
    exit 1
fi
