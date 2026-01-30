# DragonSpark.AspNetCore.AutoCert

ASP.NET Core integration for DragonSpark.AutoCert. Provides Middleware and Kestrel hooks for automatic SSL termination.

## Features

- Kestrel Certificate Selector integration.
- Automatic HTTP-01 challenge handling via middleware.
- Easy setup with `AddAutoCert`.

## Installation

```bash
dotnet add package DragonSpark.AspNetCore.AutoCert
```

## Usage

```csharp
builder.Services.AddAutoCert(options =>
{
    options.Email = "admin@example.com";
    options.TermsOfServiceAgreed = true;
});
```
