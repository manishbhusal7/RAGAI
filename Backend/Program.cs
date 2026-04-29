using Backend.Services.Search;
using Backend.Services.AI;
using Backend.Services.Integrations;
using Backend.Services.BackgroundServices;
using DotNetEnv;

// Load environment variables from .env file
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Configure environment variables to map to configuration sections
builder.Configuration.AddEnvironmentVariables();

// Helper to set config from env only when present
void SetIfNotEmpty(string configKey, string? envVal)
{
    if (!string.IsNullOrWhiteSpace(envVal))
    {
        builder.Configuration[configKey] = envVal.Trim();
    }
}

// Override configuration with environment variables (only if provided)
SetIfNotEmpty("Azure:BlobStorageConnectionString", Environment.GetEnvironmentVariable("AZURE_BLOB_CONNECTION_STRING"));
SetIfNotEmpty("Azure:BlobContainer", Environment.GetEnvironmentVariable("AZURE_BLOB_CONTAINER"));
SetIfNotEmpty("Azure:OpenAIEndpoint", Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT"));
SetIfNotEmpty("Azure:OpenAIKey", Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY"));

SetIfNotEmpty("AzureSearch:Endpoint", Environment.GetEnvironmentVariable("AZURE_SEARCH_ENDPOINT"));
SetIfNotEmpty("AzureSearch:IndexName", Environment.GetEnvironmentVariable("AZURE_SEARCH_INDEX_NAME"));
SetIfNotEmpty("AzureSearch:ApiKey", Environment.GetEnvironmentVariable("AZURE_SEARCH_API_KEY"));

SetIfNotEmpty("Confluence:BaseUrl", Environment.GetEnvironmentVariable("CONFLUENCE_BASE_URL"));
SetIfNotEmpty("Confluence:Username", Environment.GetEnvironmentVariable("CONFLUENCE_USERNAME"));
SetIfNotEmpty("Confluence:Password", Environment.GetEnvironmentVariable("CONFLUENCE_PASSWORD"));
SetIfNotEmpty("Confluence:SpaceKey", Environment.GetEnvironmentVariable("CONFLUENCE_SPACE_KEY"));

SetIfNotEmpty("MicrosoftGraph:TenantId", Environment.GetEnvironmentVariable("MS_GRAPH_TENANT_ID"));
SetIfNotEmpty("MicrosoftGraph:ClientId", Environment.GetEnvironmentVariable("MS_GRAPH_CLIENT_ID"));
SetIfNotEmpty("MicrosoftGraph:ClientSecret", Environment.GetEnvironmentVariable("MS_GRAPH_CLIENT_SECRET"));

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add HttpClient for Confluence API calls
builder.Services.AddHttpClient();

// Register services
builder.Services.AddScoped<AzureSearchService>();
builder.Services.AddScoped<AzureAIService>();
builder.Services.AddScoped<ConfluenceService>();
builder.Services.AddScoped<CalendarService>();

// Add background services for automatic syncing
builder.Services.AddHostedService<ConfluenceSyncBackgroundService>();
builder.Services.AddHostedService<CalendarSyncBackgroundService>();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        builder =>
        {
            builder.WithOrigins(
                       "http://localhost:3000",
                       "https://localhost:3000",
                       "http://localhost:4200",
                       "https://localhost:4200")
                   .AllowAnyHeader()
                   .AllowAnyMethod()
                   .AllowCredentials();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Only redirect to HTTPS in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowReactApp");

app.UseAuthorization();

app.MapControllers();

app.Run();
