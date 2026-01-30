# DragonSpark.AutoCert

Core ACME protocol logic and abstractions for DragonSpark.AutoCert.

## Features

- ACME v2 Protocol implementation (Let's Encrypt).
- Abstract definitions for Certificate and Challenge stores.
- Core services for ordering, validating, and managing certificates.

## Installation

```bash
dotnet add package DragonSpark.AutoCert
```

## Usage

This is the core package. For ASP.NET Core integration, use `DragonSpark.AspNetCore.AutoCert`.
For Entity Framework persistence, use `DragonSpark.AutoCert.EntityFramework`.
