<h1 align="center">
  DragonSpark AutoCert
</h1>

<p align="center">
    <img alt="License" src="https://img.shields.io/github/license/dragonspark-tech/DragonSpark.AutoCert?style=for-the-badge&color=blue">
</p>

<p align="center">
   <img alt="Quality Gate" src="https://img.shields.io/sonar/quality_gate/dragonspark-tech_DragonSpark.AutoCert?server=https%3A%2F%2Fsonarcloud.io&style=for-the-badge&logo=sonar">
   <img alt="Quality Gate" src="https://img.shields.io/sonar/tech_debt/dragonspark-tech_DragonSpark.AutoCert?server=https%3A%2F%2Fsonarcloud.io&style=for-the-badge&logo=sonar">
   <img alt="Quality Gate" src="https://img.shields.io/sonar/violations/dragonspark-tech_DragonSpark.AutoCert?server=https%3A%2F%2Fsonarcloud.io&style=for-the-badge&logo=sonar">
   <img alt="Quality Gate" src="https://img.shields.io/sonar/coverage/dragonspark-tech_DragonSpark.AutoCert?server=https%3A%2F%2Fsonarcloud.io&style=for-the-badge&logo=sonar">
</p>

> A modern, lightweight, and extensible ACME e.g., (Let's Encrypt) client for .NET 10+.

## 🧪 Samples

- **[Basic Sample](samples/DragonSpark.AutoCert.Sample/README.md)**: Simple setup using **File System** storage. Perfect for development and single-server apps.
- **[Hybrid Sample (Redis + SQL)](samples/DragonSpark.AutoCert.Sample.Hybrid/README.md)**: **Recommended for Production**. Demonstrates a robust, clustered installation using **Redis** (L1 Cache) and **SQL Server** (L2 Persistence) with the simplified `.UseHybridPersistence<TContext>()` API.

## 📖 Overview

DragonSpark.AutoCert is designed to make HTTPS automatic and effortless for .NET applications. Whether you are running a single service on a VPS or a complex microservices architecture in Kubernetes, this library handles the entire certificate lifecycle—ordering, validation, installation, and renewal—without manual intervention.

It bridges the gap between simple "set and forget" tools and enterprise requirements, offering robust distributed locking, clustering support, and comprehensive observability out of the box.

## 🧩 Features

- **Automatic Certificate Management**: Automatically orders, validates, and installs SSL certificates from Let's Encrypt.
- **Background Renewal**: Built-in background service monitors processing and renewing certificates before they expire.
- **Enterprise Grade**:
  - **Clustering Ready**: Native support for Redis and SQL Server distributed caching ensures seamless operation in load-balanced environments (Kubernetes, Web Farms).
  - **Distributed Locking**: Robust implementation of RedLock (Redis) and FileSystem locks to prevent race conditions during certificate renewal.
  - **Key Rollover**: Compliance-ready automated account key rotation (`RolloverAccountKeyAsync`).
- **Flexible Architecture**:
  - **Strategy Pattern**: Pluggable challenge handlers (HTTP-01 built-in, ready for DNS-01).
  - **Observability**: First-class citizen support for **OpenTelemetry**. Trace every order, validation, and renewal operation.
  - **Security First**: Configurable key algorithms (RSA/ECDSA) and strict validation.

## ⚙️ Installation

Install the NuGet package (Targeting .NET 10 LTS):

```bash
dotnet add package DragonSpark.AutoCert
dotnet add package DragonSpark.AspNetCore.AutoCert
```

## 🚩 Quick Start

### 1. Simple Setup (Single Server / Development)

Ideal for small applications running on a single server. Uses the local file system to store certificates.

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add AutoCert Services using File System
builder.Services.AddAutoCert(options =>
{
    options.Email = "admin@example.com";
    options.TermsOfServiceAgreed = true;
    options.CertificatePassword = "StrongPassword123!@#"; // REQUIRED for PFX export/protection
    options.CertificatePath = ".letsencrypt"; // Stores certs/keys in a local folder
})
;
```

### 2. Production Setup (Clustered / High Availability)

Recommended for production environments, Kubernetes, or load-balanced setups. Uses Redis for distributed storage and locking.

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add Redis Cache
builder.Services.AddStackExchangeRedisCache(o => o.Configuration = "localhost");

// Add AutoCert Services with Distributed Persistence
builder.Services.AddAutoCert(options =>
{
    options.Email = "admin@example.com";
    options.TermsOfServiceAgreed = true;
    options.CertificatePassword = "StrongPassword123!@#"; // REQUIRED for PFX export/protection
})
.PersistToDistributedCache(); // Uses Redis for Accounts, Orders, and Certs
```

### 3. Run Application

Ensure you use HTTPS. The library hooks into Kestrel automatically.

```csharp
var app = builder.Build();
app.UseHttpsRedirection();
app.Run();
```

## Configuration

You can configure options via `appsettings.json`:

```json
{
  "AutoCert": {
    "Email": "admin@example.com",
    "TermsOfServiceAgreed": true,
    "CertificateAuthority": "https://acme-staging-v02.api.letsencrypt.org/directory",
    "ValidationTimeout": "00:01:00",
    "AccountKeyId": "optional-key-id",
    "AccountHmacKey": "optional-hmac-key",
    "CertificatePassword": "StrongPassword123!@#", // MUST be at least 8 chars
    "KeyAlgorithm": "ES256" // ES256, ES384, RS256, RS4096
  }
}
```

> [!WARNING]
> **Security Requirement**: `CertificatePassword` MUST be set and be at least 8 characters long. The application will throw an exception at startup if this requirement is not met.

## 🚀 Performance & Best Practices

### Optimize TLS Handshakes

For high-traffic applications, fetching certificates from a distributed store (Redis/SQL) on every handshake can introduce latency. Use the **Layered Persistence** strategy with an in-memory cache for maximum performance.

```csharp
// 1. Add In-Memory Cache (L1)
builder.Services.AddMemoryCache();

// 2. Configure Distributed Cache (L2) - e.g., Redis
builder.Services.AddStackExchangeRedisCache(o => o.Configuration = "localhost");

// 3. Register AutoCert with Layered Store
builder.Services.AddAutoCert(...)
    .UseLayeredPersistence(); // Automatically uses MemoryCache as L1 and DistributedCache as L2
```

### Middleware Ordering

To ensure HTTP-01 challenges are handled correctly before any redirection logic, ensure `UseAutoCertChallenge()` (if manually mapped) or the automatic middleware handling takes precedence.

Since this library hooks into Kestrel directly, standard `UseHttpsRedirection` usage is generally safe, but be aware that the challenge path `/.well-known/acme-challenge/*` must remain accessible over **HTTP** (Port 80) to satisfy Let's Encrypt validators.

```csharp
var app = builder.Build();

// Ensure HTTP requests to /.well-known/acme-challenge/ are NOT redirected to HTTPS
// The library handles this automatically when using the default setup.
app.UseHttpsRedirection();

app.Run();
```

## Advanced Usage

### Persistence Strategies

**Entity Framework Custom Store:**
Store certificates in your SQL database.

```csharp
builder.Services.AddAutoCert(...)
    .AddEntityFrameworkStore<MyDbContext>();
```

**Layered Persistence (Hybrid):**
Use a distributed cache for speed (L1) and a persistent store (EF Core) for durability (L2). This is **highly recommended** for high-performance production apps to avoid network latency during TLS handshakes.

```csharp
// 1. Add Redis (L1)
builder.Services.AddStackExchangeRedisCache(...)

// 2. Add DbContext (L2)
builder.Services.AddDbContext<MyCertContext>(...)

// 3. Register AutoCert with Hybrid Persistence
builder.Services.AddAutoCert(...)
    .UseHybridPersistence<MyCertContext>(); // Automatically configures Redis as L1, SQL/EF as L2
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
builder.Services.AddAutoCert(...).AddLifecycleHook<WebHookNotifier>();
```

### Distributed Locking

**FileSystem (Default):**
Enabled by default. Stores lock files in `.locks` subdirectory.

**Redis (Clustered):**
Uses RedLock.net for robust distributed locking.

1. Add package `DragonSpark.AutoCert.Redis`.
2. Configure:

```csharp
builder.Services.AddAutoCert(...)
    .AddRedisLock("localhost:6379");
```

### Account Management

**Key Rollover:**
Rotate your account key securely:

```csharp
await acmeService.RolloverAccountKeyAsync(cancellationToken);
```

### DNS-01 Challenge Support

Support for wildcard certificates using DNS validation.

1. Implement `IDnsProvider`:

```csharp
public class MyDnsProvider : IDnsProvider
{
    public Task CreateTxtRecordAsync(string name, string value, CancellationToken token) { ... }
    public Task DeleteTxtRecordAsync(string name, string value, CancellationToken token) { ... }
}
```

2. Register and configure:

```csharp
builder.Services.AddAutoCert(o => o.DnsPropagationDelay = TimeSpan.FromSeconds(60))
    .AddDnsProvider<MyDnsProvider>();
```

## 🔭 Observability

The library supports OpenTelemetry for tracing and metrics:

- **Traces:** `DragonSpark.AutoCert` (Source)
  - Visualizes order flow, validation attempts, and DNS propagation wait times.
- **Metrics:** `DragonSpark.AutoCert` (Meter)
  - `acme.certificates.renewed` (Counter)
  - `acme.challenges.duration` (Histogram)
  - `acme.certificates.expiry_days` (Gauge)

## 🙌 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📝 License

This project is licensed under the MIT license - see the [LICENSE](LICENSE.md) file for more details.
