#pragma warning disable S4830
#pragma warning disable S1075

using System.Security.Cryptography.X509Certificates;
using DragonSpark.AspNetCore.AutoCert.Extensions;
using DragonSpark.AutoCert;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("AutoCert")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });

builder.Services.AddAutoCert(options =>
{
    options.Email = "admin@example.com";
    options.TermsOfServiceAgreed = true;

    // Use Pebble (Local ACME Server)
    // Ensure you run 'docker-compose up pebble' first
    options.CertificateAuthority = AutoCertDirectories.Pebble;

    // Required password for PFX protection
    options.CertificatePassword = "SamplePassword123!@#";

    // Stores certs/keys in a local folder
    options.CertificatePath = ".letsencrypt";

    // Domains to automatically manage (REQUIRED for renewal/ordering service)
    options.ManagedDomains.Add("localhost");

    // Fetch and add Pebble's Root CA to satisfy Certes chain building
    // In a real scenario with Let's Encrypt, this is not needed.
    try
    {
        using var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        
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
});

var app = builder.Build();

app.UseAutoCertChallenge();
app.UseHttpsRedirection();

app.MapGet("/", () => "Hello from DragonSpark.AutoCert Sample!");

await app.RunAsync();