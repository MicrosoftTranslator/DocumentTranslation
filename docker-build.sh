#!/bin/bash

# Docker Build Script for Document Translation Web Application
# This script builds the Docker image for the .NET 9 web application

set -e  # Exit on any error

# Configuration
IMAGE_NAME="document-translation-web"
IMAGE_TAG="latest"
FULL_IMAGE_NAME="${IMAGE_NAME}:${IMAGE_TAG}"

echo "🐳 Building Docker image for Document Translation Web Application"
echo "=================================================="

# Ensure we're in the right directory
if [ ! -f "Dockerfile" ]; then
    echo "❌ Error: Dockerfile not found. Make sure you're in the project root directory."
    exit 1
fi

if [ ! -f "DocumentTranslation.Web/DocumentTranslation.Web.csproj" ]; then
    echo "❌ Error: Web project not found. Make sure you're in the project root directory."
    exit 1
fi

echo "📋 Build Information:"
echo "   - Image Name: ${FULL_IMAGE_NAME}"
echo "   - .NET Version: 9.0"
echo "   - Build Context: $(pwd)"
echo ""

# Build the Docker image
echo "🔨 Building Docker image..."
docker build \
    --build-arg BUILD_CONFIGURATION=Release \
    --tag "${FULL_IMAGE_NAME}" \
    --progress=plain \
    .

if [ $? -eq 0 ]; then
    echo ""
    echo "✅ Docker image built successfully!"
    echo ""
    
    # Show image information
    echo "📊 Image Information:"
    docker images "${IMAGE_NAME}" --format "table {{.Repository}}\t{{.Tag}}\t{{.Size}}\t{{.CreatedAt}}"
    echo ""
    
    echo "🚀 To run the container:"
    echo "   docker run -d -p 8080:8080 --name document-translation ${FULL_IMAGE_NAME}"
    echo ""
    echo "🔍 To view logs:"
    echo "   docker logs document-translation"
    echo ""
    echo "🌐 Access the application at:"
    echo "   http://localhost:8080"
    echo ""
    echo "🩺 Health check endpoint:"
    echo "   http://localhost:8080/health"
    echo ""
    echo "📚 API documentation (Swagger):"
    echo "   http://localhost:8080/swagger (in development mode)"
    
else
    echo ""
    echo "❌ Docker build failed!"
    exit 1
fi
