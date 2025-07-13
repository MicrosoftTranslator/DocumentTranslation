#!/bin/bash

# Docker Run Script for Document Translation Web Application
# This script runs the Docker container with proper configuration

set -e  # Exit on any error

# Configuration
IMAGE_NAME="document-translation-web:latest"
CONTAINER_NAME="document-translation"
PORT="8080"

echo "🚀 Running Document Translation Web Application in Docker"
echo "======================================================="

# Check if Docker is available
if ! command -v docker &> /dev/null; then
    echo "❌ Error: Docker is not installed or not available."
    echo "   Please install Docker Desktop and ensure WSL integration is enabled."
    exit 1
fi

# Check if image exists
if ! docker image inspect "$IMAGE_NAME" &> /dev/null; then
    echo "❌ Error: Docker image '$IMAGE_NAME' not found."
    echo "   Please build the image first using: ./docker-build.sh"
    exit 1
fi

# Stop and remove existing container if it exists
if docker ps -a --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
    echo "🛑 Stopping existing container..."
    docker stop "$CONTAINER_NAME" || true
    echo "🗑️  Removing existing container..."
    docker rm "$CONTAINER_NAME" || true
fi

echo "📋 Container Information:"
echo "   - Image: $IMAGE_NAME"
echo "   - Container Name: $CONTAINER_NAME"
echo "   - Port: $PORT"
echo ""

# Run the container
echo "🐳 Starting container..."
docker run -d \
    --name "$CONTAINER_NAME" \
    --port "$PORT:8080" \
    --restart unless-stopped \
    --env ASPNETCORE_ENVIRONMENT=Production \
    --env ASPNETCORE_URLS=http://+:8080 \
    "$IMAGE_NAME"

if [ $? -eq 0 ]; then
    echo ""
    echo "✅ Container started successfully!"
    echo ""
    
    # Wait a moment for the application to start
    echo "⏳ Waiting for application to start..."
    sleep 5
    
    # Check container status
    if docker ps --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
        echo "✅ Container is running!"
        echo ""
        
        echo "🌐 Application URLs:"
        echo "   - Main Application: http://localhost:$PORT"
        echo "   - Health Check: http://localhost:$PORT/health"
        echo "   - API Documentation: http://localhost:$PORT/swagger"
        echo ""
        
        echo "📊 Container Status:"
        docker ps --filter "name=$CONTAINER_NAME" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
        echo ""
        
        echo "🔧 Useful Commands:"
        echo "   - View logs: docker logs $CONTAINER_NAME"
        echo "   - Follow logs: docker logs -f $CONTAINER_NAME"
        echo "   - Stop container: docker stop $CONTAINER_NAME"
        echo "   - Access container shell: docker exec -it $CONTAINER_NAME /bin/bash"
        echo ""
        
        # Test health endpoint
        echo "🩺 Testing health endpoint..."
        if curl -f "http://localhost:$PORT/health" &> /dev/null; then
            echo "✅ Health check passed!"
        else
            echo "⚠️  Health check failed or endpoint not ready yet."
            echo "   Try again in a few moments: curl http://localhost:$PORT/health"
        fi
        
    else
        echo "❌ Container failed to start properly."
        echo "📋 Container logs:"
        docker logs "$CONTAINER_NAME"
        exit 1
    fi
    
else
    echo ""
    echo "❌ Failed to start container!"
    exit 1
fi
