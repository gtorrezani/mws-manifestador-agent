# Local Run

## Prerequisites

- .NET SDK 8.0, matching `global.json`.
- PowerShell.
- The Web/API running at `http://127.0.0.1:8020`.
- MySQL and Redis running from the Web repository local compose.

## Ports

- Laravel Web/API: `http://127.0.0.1:8020`
- Agent local diagnostics: `http://127.0.0.1:8022`

## Configuration

Development settings are in `src/Mws.Manifestador.Agent.Worker/appsettings.Development.json`:

```json
{
  "AgentApi": {
    "BaseUrl": "http://127.0.0.1:8020",
    "ActivationCode": null
  },
  "AgentPolling": {
    "IntervalSeconds": 10,
    "HeartbeatIntervalSeconds": 10
  },
  "LocalDiagnostics": {
    "Enabled": true,
    "ListenUrl": "http://127.0.0.1:8022"
  }
}
```

Do not commit a real activation code or agent secret. Use environment variables or an ignored local settings file for real local activation.

## Quality Gates

```powershell
dotnet restore
dotnet build Mws.Manifestador.Agent.sln --configuration Release
dotnet test Mws.Manifestador.Agent.sln --configuration Release
dotnet format Mws.Manifestador.Agent.sln --verify-no-changes
```

## Start Agent in Console Mode

```powershell
.\scripts\local-agent.ps1
```

If PowerShell blocks local scripts, run with execution policy bypass for this process:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\local-agent.ps1
```

Manual equivalent:

```powershell
dotnet run --project src\Mws.Manifestador.Agent.Worker\Mws.Manifestador.Agent.Worker.csproj --environment Development
```

If no activation code is configured, the expected behavior is a clear warning:

```text
Agent is not activated; configure AgentApi:ActivationCode to activate it
```

## Activation Code

1. Start the Web/API and Vite.
2. Open the Agents screen in the web app.
3. Generate an activation code for a company.
4. Configure the code locally for the agent without committing it.

Example for the current PowerShell session only:

```powershell
.\scripts\local-agent.ps1 -ActivationCode "123456"
```

After successful activation, the agent stores credentials using the configured secure credential store and starts sending heartbeat/poll requests.

## Local Diagnostics

When enabled, open:

```text
http://127.0.0.1:8022/health
```

Certificate inventory exposed by the local diagnostic endpoint:

```text
http://127.0.0.1:8022/certificates
```

Full local diagnostics:

```text
http://127.0.0.1:8022/diagnostics
```

This endpoint is for local development and support diagnostics only. It listens on loopback, returns sanitized operational metadata and certificate inventory counts, and does not expose private keys, tokens, fiscal XML, passwords or PINs.

Credentials created by activation are protected with DPAPI. The default path is:

```text
%ProgramData%\MWS Manifestador Agent\agent-credentials.dpapi
```

Delete that file only when you intentionally want to force reactivation.

## A3 Certificate Notes

- Test A3 behavior first in console mode.
- Windows Service execution may not see certificates installed in `CurrentUser`.
- Do not store or log A3 PIN.
- Let the token provider/driver prompt for PIN when needed.

## Manual List Certificates Flow

1. Start the Web/API at `http://127.0.0.1:8020`.
2. Start the Agent with `.\scripts\local-agent.ps1`.
3. In the Web UI, select the company linked to the Agent.
4. Open the Certificates screen.
5. Click `Listar certificados do agente ...`.
6. The Agent polls the API, receives `list_certificates`, reads `StoreName.My` from `CurrentUser` and `LocalMachine`, and completes the command with sanitized certificate metadata.
7. The Web/API persists the inventory and shows it in the Certificates screen.

## Manual Test Certificate Flow

1. Start the Web/API at `http://127.0.0.1:8020`.
2. Start the Agent with `.\scripts\local-agent.ps1`.
3. In the Web UI, select the company linked to the Agent.
4. Open the Certificates screen.
5. Click `Listar certificados do agente ...` if the inventory is empty.
6. Click `Testar` for an A3 certificate with thumbprint and private key.
7. The Agent polls the API, receives `test_certificate`, locates the certificate by thumbprint and store location, validates metadata, and performs a disposable sign/verify operation with the private key provider.
8. The Web/API receives `complete` or `fail` and updates last test status, message and timestamp.

The Agent never stores or captures A3 PIN. If the token provider requires PIN, Windows must prompt through the provider/driver. Command results must not include private key material, passwords or PIN.

## Manual Test SEFAZ Connectivity Flow

1. Start the Web/API at `http://127.0.0.1:8020`.
2. Start the Agent with `.\scripts\local-agent.ps1`.
3. In the Web UI, select the company linked to the Agent.
4. Open the Certificates screen.
5. Ensure an A3 certificate is linked to the company and tested as valid.
6. Click `Testar SEFAZ` to run `configuration_only`.
7. The Agent polls the API, receives `test_sefaz_connectivity`, validates payload, certificate access and SEFAZ endpoint resolution, then completes the command.
8. The Web/API persists the test history and displays the status in the Certificates screen.

`configuration_only` does not call SEFAZ. `live_homologation` is deliberately returned as `SEFAZ_LIVE_TEST_NOT_CONFIGURED` until an approved non-mutating homologation probe is defined. Do not treat it as available production behavior.

## Manual Fiscal Document Sync Flow

1. Start the Web/API at `http://127.0.0.1:8020`.
2. Start the Agent with `.\scripts\local-agent.ps1`.
3. In the Web UI, select a homologation company linked to the Agent.
4. Ensure an A3 certificate is linked to the company and tested as valid.
5. Open the Fiscal Documents screen.
6. Click `Consultar SEFAZ`.
7. The Agent polls the API, receives `sync_fiscal_documents`, validates payload and certificate access, resolves the `NFeDistribuicaoDFe` endpoint, builds `distDFeInt`, sends the SOAP request and parses `retDistDFeInt`.
8. The Web/API persists NSU state, SEFAZ request/response metadata, document summaries and full XMLs when returned.

Production distribution is blocked by default. It requires explicit `Sefaz:AllowProductionDistribution=true`.

Expected SEFAZ handling:

- `cStat=137`: complete command with no documents and advance NSU from the trusted response.
- `cStat=138`: complete command, advance NSU and return normalized documents.
- `cStat=656`: fail command with `SEFAZ_DISTRIBUTION_CONSUMPTION_DENIED`; do not advance NSU.

TODO: validate SOAP 1.2 content type/action and the full request/response evidence against an official homologation environment before marking this integration production-ready.

## Stop Agent

Press `Ctrl+C` in the console running the agent.

## Windows Service

See `docs/installation/windows-service.md` for administrative install, update, uninstall, logs, DPAPI credential removal and A3 account trade-offs.
