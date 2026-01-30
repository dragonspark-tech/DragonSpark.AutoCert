# DragonSpark.AutoCert Sample Application

This sample application demonstrates how to use `DragonSpark.AutoCert` in an ASP.NET Core Minimal API. It is configured
to use **Pebble**, a local ACME testing server, to fully simulate certificate issuance locally without needing public
domains or external tunnels.

## Prerequisites

- .NET 10.0 SDK
- Docker & Docker Compose (to run Pebble)

## Getting Started

### 1. Start Pebble (ACME Server)

The repository includes a `docker-compose.yaml` file that runs Pebble. Start it from the root of the repository:

```bash
docker-compose up -d pebble
```

This starts the ACME server on `https://localhost:14000/dir`.

### 2. Configure the Application

The sample is pre-configured in `Program.cs` to:

- Use Pebble as the Certificate Authority.
- Trust Pebble's self-signed SSL certificates for ACME operations (via a custom `HttpClient`).
- Store certificates in a local `.letsencrypt` folder (**File System Persistence**).

### 3. Run the Sample

Run the application:

```bash
dotnet run --project samples/DragonSpark.AutoCert.Sample
```

### 4. Test Certificate Generation

1. **Observe Logs**: You should see logs indicating `DragonSpark.AutoCert` is ordering a certificate for `localhost` (or
   whatever domain the app binds to).
2. **Validation**: Pebble internally validates the challenge.
3. **Success**: A certificate will be issued and saved.
4. **Browser Access**: Navigate to `https://localhost:5001` (or your app's HTTPS port).
    - **Note**: Your browser will still warn about the certificate because the _issued_ certificate comes from Pebble's
      untrusted root CA. This is expected and confirms the flow is working.

## Troubleshooting

- **"Connection Refused"**: Ensure Docker is running and `docker-compose up pebble` was successful.
- **"SSL Error"**: Ensure the `AutoCert` HttpClient configuration in `Program.cs` is correctly ignoring SSL errors for
  the Pebble connection.
