using Azure.Identity;
using DocumentTranslation.Web.Services;
using DocumentTranslationService.Core;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure CORS for development and production
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure Azure services with Managed Identity
if (builder.Environment.IsProduction())
{
    builder.Services.AddSingleton<Azure.Core.TokenCredential>(new DefaultAzureCredential());
}
else
{
    // For development, use environment variables or user secrets
    builder.Services.AddSingleton<Azure.Core.TokenCredential>(new DefaultAzureCredential());
}

// Register application services
builder.Services.Configure<DocumentTranslationSettings>(
    builder.Configuration.GetSection("DocumentTranslation"));

builder.Services.AddScoped<IDocumentTranslationWebService, DocumentTranslationWebService>();
builder.Services.AddScoped<DocumentTranslationService.Core.DocumentTranslationService>();

// Add health checks
builder.Services.AddHealthChecks();

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddApplicationInsights();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseRouting();
app.UseAuthorization();

// Add health check endpoint
app.MapHealthChecks("/health");

app.MapControllers();

// Serve static files for the frontend
app.UseDefaultFiles();
app.UseStaticFiles();

// Fallback to index.html for SPA routing
app.MapFallbackToFile("index.html");

app.Run();
