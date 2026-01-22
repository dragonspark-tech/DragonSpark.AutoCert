# DragonSpark.Acme

A modern, lightweight, and extensible ACME (Let's Encrypt) client for ASP.NET Core 10+.

![Build Status](https://img.shields.io/github/actions/workflow/status/dragonspark-tech/acme/build.yml?branch=main)
![Nuget](https://img.shields.io/nuget/v/DragonSpark.Acme)
![License](https://img.shields.io/github/license/dragonspark-tech/acme)

## Features

- **Automatic Certificate Management**: Automatically orders, validates, and installs SSL certificates from Let's Encrypt.
- **Background Renewal**: Built-in background service monitors processing and renewing certificates before they expire.
- **Enterprise Ready**:
    - **Distributed Caching**: Support for Redis/SQL Server distributed caches for persistence.
    - **Entity Framework Core**: Store certificates and accounts in your database.
    - **Lifecycle Hooks**: React to new certificates (e.g., notify a cluster, reload separate services).
- **Flexible Architecture**:
    - **Strategy Pattern**: Support for different challenge types (HTTP-01 implemented, extensible for DNS-01).
    - **Feature Parity**: Inspired by `LettuceEncrypt` and `FluffySpoon` but modernized for .NET 10.

## Installation

Install the NuGet package:

```bash
dotnet add package DragonSpark.AspNetCore.Acme
```

## Quick Start

1.  **Configure Services**: Add ACME services in your `Program.cs`.

    ```csharp
    var builder = WebApplication.CreateBuilder(args);

    // Add ACME Services
    builder.Services.AddAcme(options =>
    {
        options.Email = "admin@example.com";
        options.TermsOfServiceAgreed = true;
    })
    .AddFileSystemStore(".letsencrypt"); // Store certs in a local folder
    ```

2.  **Configure Middleware**: Ensure you use HTTPS.

    ```csharp
    var app = builder.Build();

    app.UseHttpsRedirection();
    // ACME middleware is automatically handled by the hosted service and Kestrel integration.
    
    app.Run();
    ```

3.  **Run**: Start your application exposed to the internet on Port 80 (for HTTP-01 challenge). The library will automatically provision a certificate for your configured domains.

## Configuration

You can configure options via `appsettings.json`:

```json
{
  "Acme": {
    "Email": "admin@example.com",
    "TermsOfServiceAgreed": true,
    "CertificateAuthority": "https://acme-staging-v02.api.letsencrypt.org/directory",
    "ValidationTimeout": "00:01:00",
    "CertificatePassword": "ultra-secure-password"
  }
}
```

## Advanced Usage

### Persistence Strategies

**Entity Framework Custom Store:**
```csharp
builder.Services.AddAcme(...)
    .AddEntityFrameworkStore<MyDbContext>();
```

**Distributed Cache (Redis):**
```csharp
builder.Services.AddStackExchangeRedisCache(o => o.Configuration = "localhost");
builder.Services.AddAcme(...)
    .PersistToDistributedCache();
```

### Lifecycle Hooks
Run custom logic when a certificate is created or renewed:

```csharp
public class WebHookNotifier : ICertificateLifecycle
{
    public async Task OnCertificateCreatedAsync(string domain, X509Certificate2 cert, CancellationToken token)
    {
        // Call an external webhook
    }
    
    public Task OnRenewalFailedAsync(string domain, Exception error, CancellationToken token) { ... }
}

// Register
builder.Services.AddAcme(...).AddLifecycleHook<WebHookNotifier>();
```

### Functional Persistence
For quick/simple scenarios, use delegates instead of classes:

```csharp
builder.Services.AddAcme(...)
    .AddCertificateStore(
        load: (domain, token) => AzureKeyVault.GetSecretAsync(domain),
        save: (domain, cert, token) => AzureKeyVault.SetSecretAsync(domain, cert)
    );
```

## Contributing

Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

## License

[MIT](LICENSE)
