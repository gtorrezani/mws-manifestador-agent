# Quality Gates

Run locally:

```powershell
dotnet restore Mws.Manifestador.Agent.sln
dotnet build Mws.Manifestador.Agent.sln --configuration Release
dotnet format Mws.Manifestador.Agent.sln --verify-no-changes --no-restore
dotnet test Mws.Manifestador.Agent.sln --configuration Release --no-build
```

Rules:

- `Nullable`, `TreatWarningsAsErrors`, `AnalysisMode=AllEnabledByDefault`, and `EnforceCodeStyleInBuild` stay enabled.
- StyleCop, SonarAnalyzer.CSharp, Meziantou.Analyzer, and Roslynator.Analyzers run during build.
- Build must fail on warnings.
- Fiscal status and event logic must use enums/value objects.
- Tests must cover manifestation event codes, HMAC authentication, command idempotency, and command lock behavior.
- Do not commit real fiscal XML, certificate files, private keys, token PINs, access keys, CNPJs, HMAC secrets, or passwords.
- Logs must avoid XML payloads and sensitive values by default.
