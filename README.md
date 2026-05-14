# MWS Manifestador NF-e Agent

Windows Worker Service for the MWS Manifestador NF-e product.

## Architecture

- `Mws.Manifestador.Agent.Domain`: pure domain types, enums, value objects and exceptions.
- `Mws.Manifestador.Agent.Application`: use cases, command abstractions, polling, heartbeat and interfaces.
- `Mws.Manifestador.Agent.Infrastructure`: Laravel API client, HMAC signing, DPAPI credential store and local XML storage.
- `Mws.Manifestador.Agent.Sefaz`: Windows Certificate Store, XML Signature, SOAP transport and explicit SEFAZ integration boundaries.
- `Mws.Manifestador.Agent.Worker`: Windows Service host and orchestration only.
- `Mws.Manifestador.Agent.Tests`: xUnit tests.

## Security

The agent stores API credentials with Windows DPAPI using `DataProtectionScope.LocalMachine`.
It does not store or capture A3 PINs; the token provider/driver prompts for PIN when required. A1 support is prepared through a separate `CertificateReference` and protected `CertificateSecret` model, so plaintext PFX passwords are not persisted. Fiscal integrations that still require official endpoint/schema decisions throw explicit errors instead of returning fake fiscal results.

## Local Run

```powershell
dotnet run --project src/Mws.Manifestador.Agent.Worker/Mws.Manifestador.Agent.Worker.csproj
```

Set `AgentApi:BaseUrl` and `AgentApi:ActivationCode` via `appsettings.Development.json`, environment variables or user secrets.

## Publish

```powershell
dotnet publish src/Mws.Manifestador.Agent.Worker/Mws.Manifestador.Agent.Worker.csproj -c Release -r win-x64 --self-contained false -o publish/agent
.\scripts\install-service.ps1 -PublishDirectory .\publish\agent
```
