# DragonSpark.AutoCert.Redis

Redis integration for DragonSpark.AutoCert. Provides distributed locking and caching.

## Features

- Distributed locking using RedLock.net.
- High-performance caching for certificates.

## Installation

```bash
dotnet add package DragonSpark.AutoCert.Redis
```

## Usage

```csharp
builder.Services.AddAutoCert(...)
    .UseRedisLocks(connectionMultiplexer);
```
