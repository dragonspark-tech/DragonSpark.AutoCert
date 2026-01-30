#pragma warning disable S4830
#pragma warning disable S1075

using System.Security.Cryptography.X509Certificates;
using DragonSpark.AspNetCore.AutoCert.Extensions;
using DragonSpark.AutoCert;
using DragonSpark.AutoCert.Sample.Hybrid;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure HttpClient for Pebble (Development only)
builder.Services.AddHttpClient("AutoCert")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

// 2. Configure Infrastructure (SQL Server + Redis)
var sqlConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                          ??
                          "Server=localhost,1433;Database=AutoCertHybrid;User Id=sa;Password=Password123!;TrustServerCertificate=True;";
var redisConnection = builder.Configuration.GetConnectionString("Redis")
                      ?? "localhost:6379";

builder.Services.AddDbContext<HybridCertContext>(options =>
    options.UseSqlServer(sqlConnectionString));

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnection;
    options.InstanceName = "AutoCert:";
});

// 3. Register AutoCert
builder.Services.AddAutoCert(options =>
    {
        options.Email = "admin@hybrid.com";
        options.TermsOfServiceAgreed = true;
        options.CertificateAuthority = AutoCertDirectories.Pebble;
        options.CertificatePassword = "HybridPassword123!";
        options.ManagedDomains.Add("localhost");

        try
        {
            using var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            using var http = new HttpClient(handler);
            Console.WriteLine("Fetching Pebble Root CA...");
            var rootCaBytes = http.GetByteArrayAsync("https://localhost:15000/roots/0").GetAwaiter().GetResult();
            var rootCert = X509CertificateLoader.LoadCertificate(rootCaBytes);
            options.AdditionalIssuers.Add(rootCert.RawData);
            Console.WriteLine($"Added Pebble Root CA: {rootCert.Subject}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to fetch Pebble Root CA: {ex.Message}");
        }
    })
    .UseHybridPersistence<HybridCertContext>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HybridCertContext>();
    await db.Database.EnsureCreatedAsync();
}

app.UseAutoCertChallenge();
app.UseHttpsRedirection();

app.MapGet("/", () => "Hello from DragonSpark.AutoCert Hybrid Sample (SQL + Redis)!");

await app.RunAsync();