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

This endpoint is for local development and support diagnostics only. It returns certificate metadata from the Windows Certificate Store and does not expose private keys or PINs.

## A3 Certificate Notes

- Test A3 behavior first in console mode.
- Windows Service execution may not see certificates installed in `CurrentUser`.
- Do not store or log A3 PIN.
- Let the token provider/driver prompt for PIN when needed.

## Stop Agent

Press `Ctrl+C` in the console running the agent.
