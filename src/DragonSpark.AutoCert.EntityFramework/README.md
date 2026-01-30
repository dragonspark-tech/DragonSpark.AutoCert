# DragonSpark.AutoCert.EntityFramework

Entity Framework Core persistence layer for DragonSpark.AutoCert.

## Features

- Persist ACME accounts and certificates to any EF Core supported database.
- Distributed locking support (when combined with a distributed lock provider).

## Installation

```bash
dotnet add package DragonSpark.AutoCert.EntityFramework
```

## Usage

```csharp
builder.Services.AddAutoCert(...)
    .UseEntityFrameworkStore<MyDbContext>();
```
