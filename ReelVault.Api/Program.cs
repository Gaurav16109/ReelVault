using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ReelVault.Api;
using ReelVault.Api.Enrichment;
using ReelVault.Api.Extraction;
using ReelVault.Api.Thumbnails;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    // ItemStatus (e.g. in SavedItemDetailDto) reads as "Wishlist" instead of 0 over the wire.
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<ReelVaultDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ReelVaultDb")));

// Depend on ILlmExtractor everywhere so a different provider (e.g. Ollama) can be swapped in later.
builder.Services.AddHttpClient<ILlmExtractor, GeminiExtractor>(client =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
});

// Best-effort thumbnail lookup; never blocks a save (see Thumbnails/OEmbedThumbnailFetcher.cs).
builder.Services.AddHttpClient<IThumbnailFetcher, OEmbedThumbnailFetcher>(client =>
{
    client.BaseAddress = new Uri("https://graph.facebook.com/");
});

// On-demand only (POST /api/items/{id}/enrich) - never called from the save path. Depend on
// IPlaceEnricher everywhere so the engine stays swappable (see Enrichment/IPlaceEnricher.cs).
builder.Services.AddHttpClient<IPlaceEnricher, GooglePlacesEnricher>(client =>
{
    client.BaseAddress = new Uri("https://places.googleapis.com/");
});

// Permissive for Phase 0 local development only; the MAUI app calls the API from a different origin/port.
const string CorsPolicy = "AllowMauiApp";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors(CorsPolicy);

app.UseAuthorization();

app.MapControllers();

app.Run();
