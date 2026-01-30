# DragonSpark.AutoCert Hybrid Sample (SQL + Redis)

This sample demonstrates the **Hybrid Persistence** capabilities of `DragonSpark.AutoCert`. It combines **Redis** (for
high-performance caching) and **SQL Server** (for reliable long-term persistence) to store ACME accounts and
certificates.

## Features

- **Layered Storage**:
    - **L1 (Cache)**: Redis. Fast access for frequent reads (e.g., handshake certificate selection).
    - **L2 (Persistence)**: SQL Server (via Entity Framework Core). Durable storage for certificates and account keys.
- **Simplified Setup**: Uses the `.UseHybridPersistence<TContext>()` extension method to automatically wire up the
  complex layered dependency injection.
- **Certificate Reuse**: Automatically reuses valid certificates from SQL/Redis before ordering new ones.

## Prerequisites

- .NET 10.0 SDK
- Docker & Docker Compose

## Getting Started

### 1. Start Infrastructure

Use the provided `docker-compose.yaml` in the root of the repository to start **Pebble** (ACME Server), **Redis**, and *
*SQL Server**.

```bash
docker-compose up -d
```

This starts:

- **Pebble**: `https://localhost:14000/dir`
- **Redis**: `localhost:6379`
- **SQL Server**: `localhost,1433` (Password: `Password123!`)

### 2. Run the Sample

Run the application:

```bash
dotnet run --project samples/DragonSpark.AutoCert.Sample.Hybrid
```

### 3. How It Works

The application uses `AutoCert` with the `UseHybridPersistence` extension:

```csharp
builder.Services.AddAutoCert(options => { /* ... */ })
       .UseHybridPersistence<HybridCertContext>();
```

This registers:

- `LayeredCertificateStore` as the primary `ICertificateStore`.
- `DistributedCertificateStore` (Redis) as the Cache layer.
- A Singleton wrapper around `EfCertificateStore` (SQL) as the Persistence layer.

### 4. Verification

1. **First Run**:
    - The app connects to SQL Server (creates DB `AutoCertHybrid` if missing).
    - Checks Redis (Empty).
    - Checks SQL (Empty).
    - Orders a new certificate from Pebble.
    - Saves to SQL and Redis.
2. **Subsequent Runs**:
    - App starts.
    - Checks Redis/SQL.
    - Finds existing valid certificate.
    - Reuses it (no call to Pebble).

## Troubleshooting

- **Database Errors**: Ensure SQL Server is running and accessible at `localhost,1433`. The sample uses a hardcoded
  connection string for local development:
  `Server=localhost,1433;Database=AutoCertHybrid;User Id=sa;Password=Password123!;TrustServerCertificate=True;`.
- **Redis Errors**: Ensure Redis is running at `localhost:6379`.
