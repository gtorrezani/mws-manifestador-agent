# Quality

## Local gates

Run before every commit:

```powershell
scripts/quality.ps1
```

The script executes:

```powershell
dotnet restore Mws.Manifestador.Agent.sln
dotnet build Mws.Manifestador.Agent.sln --configuration Release --no-restore
dotnet format Mws.Manifestador.Agent.sln --verify-no-changes --no-restore
dotnet test Mws.Manifestador.Agent.sln --configuration Release --no-build
```

## PR criteria

- Restore, Release build, format verification, and tests must pass before review, or the PR must document an environment-only blocker with the exact failing command.
- New behavior requires focused tests in the closest layer: domain/application tests for pure logic, infrastructure tests for persistence/API behavior, and integration-style tests for Windows/SEFAZ boundaries when feasible.
- Public payloads, command names, storage formats, Windows service behavior, and fiscal behavior must not change without explicit regression coverage.
- Refactoring must be incremental: keep changes small, behavior-preserving, and tied to the tests that protect the touched surface.

## Lint, analyzers, and format

- `Nullable`, `TreatWarningsAsErrors`, `CodeAnalysisTreatWarningsAsErrors`, `AnalysisMode=AllEnabledByDefault`, and `EnforceCodeStyleInBuild` stay enabled.
- Meziantou.Analyzer, Roslynator.Analyzers, SonarAnalyzer.CSharp, and StyleCop.Analyzers run during build.
- `dotnet format --verify-no-changes` is the formatting gate.
- Zero warning policy: warnings are build failures and must be fixed or explicitly justified in review.
- Do not lower analyzer levels, disable warnings broadly, or add suppressions just to make a build pass.

## Secrets and fiscal data

- Never commit `.env` files, real certificates, private keys, PEM files, PFX/P12 files, certificate PINs, A1 passwords, HMAC secrets, access keys, real CNPJs, or real fiscal XML.
- Never log PINs, A1 passwords, HMAC secrets, PFX/P12 contents, private keys, full fiscal XML, or other sensitive fiscal payloads.
- Fixtures must be sanitized and minimal. Use fake values that cannot be confused with production fiscal data.

## Laravel API HMAC contract

The agent client must generate signatures compatible with the Laravel server. The shared canonical string is:

```text
METHOD
PATH
TIMESTAMP
NONCE
BODY_SHA256
```

The client sends:

```text
X-MWS-Agent-Id
X-MWS-Timestamp
X-MWS-Nonce
X-MWS-Body-SHA256
X-MWS-Signature
```

`Mws.Manifestador.Agent.Infrastructure.Api.HmacSignatureService` is the client-side contract implementation. The fixture `tests/Mws.Manifestador.Agent.Tests/Fixtures/agent-hmac-contract.json` must stay compatible with the Laravel Web/API fixture, and `tests/Mws.Manifestador.Agent.Tests/Infrastructure/HmacSignatureServiceTests.cs` verifies the body hash and signature.

Do not change header names, the canonical string order, body hashing, or signature algorithm unless both repositories are updated in the same change with contract tests and documentation.

## Protected areas

Changes in these areas require regression tests:

- HMAC signing, activation, polling, command lifecycle, heartbeat, diagnostics, and log upload.
- DPAPI credential storage, local XML/status storage, and any secret handling.
- Certificate discovery, A1/A3 validation, PIN/password handling, and Windows Certificate Store integration.
- SEFAZ SOAP transport, XML signing, manifestation event mapping, fiscal status transitions, and any code that handles full fiscal XML.
