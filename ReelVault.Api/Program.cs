using Microsoft.EntityFrameworkCore;
using ReelVault.Api;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<ReelVaultDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ReelVaultDb")));

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
