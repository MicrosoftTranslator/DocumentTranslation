# Docker Build Guide for Document Translation Web Application

## Prerequisites

1. **Docker Installation**
   - Install Docker Desktop for your operating system
   - For WSL2 users: Enable WSL2 integration in Docker Desktop settings
   - Verify installation: `docker --version`

2. **Project Requirements**
   - .NET 9.0 SDK (for development)
   - All project dependencies already configured

## Quick Start

### Option 1: Using the Build Script (Recommended)
```bash
# Build the Docker image
./docker-build.sh

# Run the container
./docker-run.sh
```

### Option 2: Manual Docker Commands
```bash
# Build the image
docker build -t document-translation-web:latest .

# Run the container
docker run -d -p 8080:8080 --name document-translation document-translation-web:latest
```

## Docker Image Details

### Base Images
- **Runtime**: `mcr.microsoft.com/dotnet/aspnet:9.0`
- **Build**: `mcr.microsoft.com/dotnet/sdk:9.0`

### Image Features
- ✅ .NET 9.0 runtime optimized
- ✅ Multi-stage build for smaller image size
- ✅ Non-root user for security
- ✅ Health check endpoint configured
- ✅ Production-ready configuration

### Exposed Ports
- **8080**: Main application port
- **8081**: HTTPS port (if configured)

## Build Process

The Docker build follows these stages:

1. **Base Stage**: Sets up the runtime environment
2. **Build Stage**: Restores NuGet packages and compiles the application
3. **Publish Stage**: Creates the optimized publish output
4. **Final Stage**: Creates the minimal runtime image

### Build Arguments
- `BUILD_CONFIGURATION`: Release (default) or Debug

## Running the Container

### Basic Run
```bash
docker run -d -p 8080:8080 --name document-translation document-translation-web:latest
```

### With Environment Variables
```bash
docker run -d -p 8080:8080 \
  --name document-translation \
  --env DocumentTranslation__AzureResourceName="your-translator-name" \
  --env DocumentTranslation__AzureRegion="eastus" \
  document-translation-web:latest
```

### With Volume Mounts (for local storage)
```bash
docker run -d -p 8080:8080 \
  --name document-translation \
  --volume /local/path:/app/data \
  document-translation-web:latest
```

## Environment Variables

| Variable | Description | Required | Default |
|----------|-------------|----------|---------|
| `ASPNETCORE_ENVIRONMENT` | Environment (Development/Production) | No | Production |
| `ASPNETCORE_URLS` | Binding URLs | No | http://+:8080 |
| `DocumentTranslation__AzureResourceName` | Azure Translator resource name | Yes* | - |
| `DocumentTranslation__SubscriptionKey` | Azure Translator key | Yes* | - |
| `DocumentTranslation__AzureRegion` | Azure region | Yes* | - |
| `DocumentTranslation__StorageConnectionString` | Storage connection string | Yes* | - |
| `DocumentTranslation__AzureKeyVaultName` | Key Vault name | No | - |

*Required for full functionality. Can use Azure Key Vault for secret management.

## Health Checks

The container includes a health check endpoint:

```bash
# Check health
curl http://localhost:8080/health

# Expected response: "Healthy"
```

Health check configuration in Docker:
- **Interval**: 30 seconds
- **Timeout**: 30 seconds
- **Start Period**: 5 seconds
- **Retries**: 3

## Accessing the Application

Once the container is running:

- **Main Application**: http://localhost:8080
- **Health Check**: http://localhost:8080/health
- **API Documentation**: http://localhost:8080/swagger (if in Development mode)

## Troubleshooting

### Container Won't Start
```bash
# Check container logs
docker logs document-translation

# Check container status
docker ps -a

# Access container shell for debugging
docker exec -it document-translation /bin/bash
```

### Common Issues

1. **Port Already in Use**
   ```bash
   # Use a different port
   docker run -d -p 8081:8080 --name document-translation document-translation-web:latest
   ```

2. **Permission Issues**
   ```bash
   # The container runs as non-root user by default
   # If you need to access files, ensure proper permissions
   ```

3. **Memory Issues**
   ```bash
   # Increase Docker memory limit in Docker Desktop settings
   # Or run with memory limit
   docker run -d -p 8080:8080 --memory="1g" --name document-translation document-translation-web:latest
   ```

## Container Management

### Start/Stop/Restart
```bash
# Stop container
docker stop document-translation

# Start container
docker start document-translation

# Restart container
docker restart document-translation

# Remove container
docker rm document-translation

# Remove image
docker rmi document-translation-web:latest
```

### View Logs
```bash
# View all logs
docker logs document-translation

# Follow logs (real-time)
docker logs -f document-translation

# View last 100 lines
docker logs --tail 100 document-translation
```

### Resource Usage
```bash
# View resource usage
docker stats document-translation

# View container details
docker inspect document-translation
```

## Production Deployment

For production deployment, consider:

1. **Use Azure Container Apps** (recommended for Azure)
2. **Use Kubernetes** for orchestration
3. **Configure secrets management** with Azure Key Vault
4. **Set up monitoring** with Application Insights
5. **Configure load balancing** for high availability
6. **Implement CI/CD pipelines** for automated deployment

## Security Considerations

- ✅ Container runs as non-root user
- ✅ Minimal base image (aspnet runtime only)
- ✅ No unnecessary packages installed
- ✅ Environment variables for sensitive data
- ✅ Health checks for monitoring
- ⚠️ Use HTTPS in production
- ⚠️ Configure proper firewall rules
- ⚠️ Regular security updates

## Performance Optimization

- Image uses multi-stage build for smaller size
- Only runtime dependencies included in final image
- Application compiled for Release mode
- Ready for horizontal scaling
- Optimized for Azure Container Apps

## Next Steps

1. **Build and test locally** using the provided scripts
2. **Deploy to Azure Container Apps** using `azd up`
3. **Configure Azure services** (Key Vault, Storage, Translator)
4. **Set up monitoring** and logging
5. **Implement CI/CD pipeline** for automated deployments
