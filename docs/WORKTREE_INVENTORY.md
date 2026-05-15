# Pending Worktree Inventory

Execution date: 2026-05-15 15:15:25 -03:00

Repository: `mws-manifestador-agent`

Current branch: `codex/quality-baseline-hmac-contract`

Current HEAD: `66fc8323eb2c022190524342c2d1b1055c5664b5`

Context: the quality baseline branch was already merged into `main` as `84a90272d181900a77a10aa36b258afb2e77b4c6`. The changes inventoried here are local pending work and are not part of that published baseline.

## Status Summary

The worktree contains 23 tracked modified files and 9 untracked files. No generated installer binaries, logs, `.env` files, certificate files, fiscal XML files, or obvious secret files were found by filename scan.

Local quality gate was executed after the safety scan:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality.ps1
```

Result:

- restore: passed
- build Release: passed
- format verify: passed
- tests: passed, 75 passed, 0 failed, 0 skipped
- warnings/errors: 0 warnings, 0 errors

## Sensitive Material Review

Filename scan found no pending `.env`, `.pfx`, `.p12`, `.pem`, `.key`, `.cer`, `.crt`, fiscal `.xml`, `.log`, `.msi`, `.exe`, `.zip`, or `.nupkg` files.

Content marker scan found expected references to certificate, PIN, password, token, secret, private key, XML fiscal, and related terms in documentation, tests, and code paths that explicitly handle or sanitize those concepts. No actual sensitive value was printed or identified. The new local status service sanitizes error messages containing sensitive terms before persisting `status.json`.

## File Inventory

| File | Category | Diff summary | Risk | Dependencies | Decision |
| --- | --- | --- | --- | --- | --- |
| `Mws.Manifestador.Agent.sln` | B/L | Adds `Mws.Manifestador.Agent.Tray` project to the solution. | Medium: expands build surface. | Depends on tray project files. | Commit with tray monitor block. |
| `docs/installation/agent-configurator-gui.md` | J | Documents local status, heartbeat/poll timestamps, and Tray Monitor operations. | Low. | Depends on local status/tray behavior being accepted. | Commit with docs block after functional commits. |
| `docs/installation/agent-installation-and-operations.md` | J | Documents Worker + Configurator + Tray installation, Start Menu entries, and sanitized `status.json`. | Low. | Depends on installer/tray/status blocks. | Commit with docs block after functional commits. |
| `docs/installation/installer-build.md` | J | Documents publishing Tray, installer version `1.0.1`, shortcuts, startup behavior, and `status.json`. | Low. | Depends on installer package changes. | Commit with docs/installer block. |
| `docs/installation/windows-service.md` | J | Documents Tray Monitor menu, local status file, and allowed fields. | Low. | Depends on tray/local status implementation. | Commit with docs block after functional commits. |
| `installer/wix/Package.wxs` | A | Adds Tray executable component, Start Menu shortcuts, Startup Folder shortcut, logs shortcut, and feature refs. | Medium/high: installer behavior and HKCU shortcut semantics need review. | Depends on tray executable and build script publishing it. | Commit with installer packaging block. |
| `scripts/build-installer.ps1` | A/J | Publishes Tray project and excludes Tray executable from generated component harvesting. | Medium: affects installer build output. | Depends on tray project existing. | Commit with installer packaging block. |
| `src/Mws.Manifestador.Agent.Application/Certificates/CertificateSummary.cs` | F | Extends certificate summary with common name, document, classification, flags, rejection reasons, warnings. | Medium: public internal contract change affects handlers/tests. | Depends on Windows provider and command output changes. | Commit with certificate inventory block. |
| `src/Mws.Manifestador.Agent.Application/Certificates/CertificateValidator.cs` | F | Rejects certificates that are not fiscal candidates. | Medium/high: behavior change for certificate validation. | Depends on certificate classification fields. | Commit with certificate inventory block. |
| `src/Mws.Manifestador.Agent.Application/Commands/ListCertificatesCommandHandler.cs` | E/F | Supports `include_expired`/`include_rejected`, filters certificates, emits expanded payload. | Medium/high: API payload contract with Web. | Depends on `ListedCertificate` and provider classification. | Commit with certificate inventory block and Web contract review. |
| `src/Mws.Manifestador.Agent.Application/Commands/ListedCertificate.cs` | E/F | Extends listed certificate DTO with classification and detail fields. | Medium/high: API payload contract with Web. | Depends on handler and Web expectations. | Commit with certificate inventory block. |
| `src/Mws.Manifestador.Agent.Configurator/MainWindow.xaml` | C/B | Enlarges UI, separates local status from operation status, adds start service/logs/refresh buttons. | Medium: desktop UX and service permission behavior. | Depends on `AgentLocalStatusService`. | Commit with configurator/local status block. |
| `src/Mws.Manifestador.Agent.Configurator/MainWindow.xaml.cs` | C/H | Uses local status service, writes sanitized activation status, starts/restarts service, opens logs, displays status. | Medium/high: service control and persisted local status. | Depends on local status service and DI package. | Commit with configurator/local status block. |
| `src/Mws.Manifestador.Agent.Infrastructure/InfrastructureServiceCollectionExtensions.cs` | H/L | Registers `AgentLocalStatusService`. | Low/medium. | Depends on local status service files. | Commit with local status block. |
| `src/Mws.Manifestador.Agent.Infrastructure/Mws.Manifestador.Agent.Infrastructure.csproj` | H/D | Adds `System.ServiceProcess.ServiceController`. | Medium: new package dependency. | Required by local service control/status. | Commit with local status block. |
| `src/Mws.Manifestador.Agent.Infrastructure/LocalStatus/AgentLocalStatusService.cs` | H/D | Adds ProgramData status read/write, service control, logs/configurator helpers, diagnostics text, and sensitive error sanitization. | Medium/high: local filesystem and service control behavior. | Used by worker, configurator, tray, tests. | Commit with local status block before tray/configurator. |
| `src/Mws.Manifestador.Agent.Infrastructure/LocalStatus/AgentLocalStatusSnapshot.cs` | H | Defines persisted sanitized local status payload. | Medium: schema should stay free of secrets. | Used by service/tray/configurator. | Commit with local status block. |
| `src/Mws.Manifestador.Agent.Infrastructure/LocalStatus/AgentLocalStatusUpdate.cs` | H | Defines partial status update payload. | Low/medium. | Used by service/worker/configurator. | Commit with local status block. |
| `src/Mws.Manifestador.Agent.Infrastructure/LocalStatus/AgentServiceState.cs` | H | Defines local service state enum. | Low. | Used by local status service. | Commit with local status block. |
| `src/Mws.Manifestador.Agent.Sefaz/Certificates/WindowsCertificateProvider.cs` | F/G | Adds ICP-Brasil/fiscal classification, CPF/CNPJ extraction, EKU/KU checks, CA/system/expired classification, rejection reasons. | High: certificate eligibility behavior and Windows Store parsing. | Depends on expanded `CertificateSummary`; tests need focused coverage. | Commit with certificate inventory block. |
| `src/Mws.Manifestador.Agent.Tray/Mws.Manifestador.Agent.Tray.csproj` | B | Adds WinForms Tray project. | Medium: new desktop process. | Depends on local status service. | Commit with tray block after local status. |
| `src/Mws.Manifestador.Agent.Tray/Program.cs` | B | Starts WinForms tray context. | Low/medium. | Depends on tray context/local status. | Commit with tray block. |
| `src/Mws.Manifestador.Agent.Tray/TrayApplicationContext.cs` | B | Adds NotifyIcon menu for configurator, service start/restart/stop, logs, diagnostics copy, exit. | Medium/high: service operations and user-visible behavior. | Depends on local status service. | Commit with tray block. |
| `src/Mws.Manifestador.Agent.Tray/TrayResources.cs` | B | Adds tray app display name. | Low. | Tray context. | Commit with tray block. |
| `src/Mws.Manifestador.Agent.Worker/Services/AgentWorker.cs` | D/H | Writes local status on startup, activation, heartbeat, polling, and operational errors. | Medium: worker loop behavior and failure handling. | Depends on local status service and DI registration. | Commit with worker/local status block. |
| `tests/Fixtures/list-certificates-result.json` | I/F | Updates fixture to expanded certificate classification payload and removes previous expired item. | Medium: fixture changes API expectations. | Depends on certificate DTO/handler changes. | Commit with certificate tests block. |
| `tests/Mws.Manifestador.Agent.Tests/Application/AgentDiagnosticsCommandHandlerTests.cs` | I/F | Updates fake certificate summary to include classification fields. | Low/medium. | Depends on expanded summary constructor. | Commit with certificate test adaptations. |
| `tests/Mws.Manifestador.Agent.Tests/Application/ListCertificatesCommandHandlerTests.cs` | I/F | Covers filtering rejected/expired certificates and expanded classification payload. | Medium. | Depends on handler/DTO changes. | Commit with certificate inventory block. |
| `tests/Mws.Manifestador.Agent.Tests/Application/TestCertificateCommandHandlerTests.cs` | I/F | Updates fake certificate summary to include classification fields. | Low/medium. | Depends on expanded summary constructor. | Commit with certificate test adaptations. |
| `tests/Mws.Manifestador.Agent.Tests/Certificates/CertificateValidatorTests.cs` | I/F | Updates fake certificate summary to include classification fields. | Low/medium. | Should add explicit rejection test in next block if not already present. | Commit with certificate inventory block. |
| `tests/Mws.Manifestador.Agent.Tests/Infrastructure/AgentLocalStatusServiceTests.cs` | I/H | Adds tests for sanitized status payload, credential exclusion, missing service state. | Medium: good coverage for sensitive local status. | Depends on local status service. | Commit with local status block. |
| `tests/Mws.Manifestador.Agent.Tests/Sefaz/TestSefazConnectivityCommandHandlerTests.cs` | I/F/G | Updates fake certificate summary to include classification fields. | Low/medium. | Depends on expanded summary constructor. | Commit with certificate test adaptations. |

## Suggested Commit Blocks

### 1. `feat: add sanitized local agent status`

Files:

- `src/Mws.Manifestador.Agent.Infrastructure/Mws.Manifestador.Agent.Infrastructure.csproj`
- `src/Mws.Manifestador.Agent.Infrastructure/InfrastructureServiceCollectionExtensions.cs`
- `src/Mws.Manifestador.Agent.Infrastructure/LocalStatus/*`
- `src/Mws.Manifestador.Agent.Worker/Services/AgentWorker.cs`
- `tests/Mws.Manifestador.Agent.Tests/Infrastructure/AgentLocalStatusServiceTests.cs`

Objective: introduce a sanitized local status file under ProgramData and let the Worker update it during lifecycle events.

Risk: medium/high because it writes local files and records operational errors. Verify that no credential, token, PIN, PFX, private key, activation code, or fiscal XML can be persisted.

Required tests:

- `dotnet test Mws.Manifestador.Agent.sln --configuration Release --no-build --filter AgentLocalStatusServiceTests`
- full `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality.ps1`

Dependencies: none. This is the foundation for Configurator and Tray.

### 2. `feat: enhance windows certificate inventory classification`

Files:

- `src/Mws.Manifestador.Agent.Application/Certificates/CertificateSummary.cs`
- `src/Mws.Manifestador.Agent.Application/Certificates/CertificateValidator.cs`
- `src/Mws.Manifestador.Agent.Application/Commands/ListCertificatesCommandHandler.cs`
- `src/Mws.Manifestador.Agent.Application/Commands/ListedCertificate.cs`
- `src/Mws.Manifestador.Agent.Sefaz/Certificates/WindowsCertificateProvider.cs`
- `tests/Fixtures/list-certificates-result.json`
- certificate-related test updates under `tests/Mws.Manifestador.Agent.Tests/Application`, `tests/Mws.Manifestador.Agent.Tests/Certificates`, and `tests/Mws.Manifestador.Agent.Tests/Sefaz`

Objective: classify Windows Store certificates as fiscal candidates, rejected/system/CA/expired, and expose a richer inventory payload to the Web/API.

Risk: high because certificate eligibility affects A3 usage and Web contract. Review ICP-Brasil heuristics, EKU/KU rules, expired filtering, and payload compatibility with the already-merged Web backend/UI.

Required tests:

- `dotnet test Mws.Manifestador.Agent.sln --configuration Release --no-build --filter ListCertificatesCommandHandlerTests`
- `dotnet test Mws.Manifestador.Agent.sln --configuration Release --no-build --filter CertificateValidatorTests`
- full `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality.ps1`

Dependencies: independent of Tray, but should be reviewed with Web certificate classification contract.

### 3. `feat: improve agent configurator local operations`

Files:

- `src/Mws.Manifestador.Agent.Configurator/MainWindow.xaml`
- `src/Mws.Manifestador.Agent.Configurator/MainWindow.xaml.cs`

Objective: display local status, start/restart service, open logs, and write activation status through the local status service.

Risk: medium/high because service control can require elevation and UX must not imply that secrets are visible.

Required tests:

- full `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality.ps1`
- manual Windows smoke test as administrator and as a standard user.

Dependencies: commit 1.

### 4. `feat: add agent tray monitor`

Files:

- `Mws.Manifestador.Agent.sln`
- `src/Mws.Manifestador.Agent.Tray/*`

Objective: add a WinForms NotifyIcon tray monitor that opens Configurator/logs, controls the service, and copies sanitized diagnostics.

Risk: medium/high because it adds a local desktop process and service control actions.

Required tests:

- full `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality.ps1`
- manual tray smoke test on Windows.

Dependencies: commit 1. Installer changes should stay out of this commit.

### 5. `feat: package tray monitor in windows installer`

Files:

- `installer/wix/Package.wxs`
- `scripts/build-installer.ps1`
- possibly installer docs from the docs block if preferred.

Objective: publish Tray in the MSI, add Start Menu shortcuts, logs shortcut, and startup shortcut.

Risk: high because installer shortcut scope, HKCU registry values, Startup Folder behavior, upgrades, and uninstall cleanup need review.

Required tests:

- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build-installer.ps1 -ProductVersion 1.0.1`
- install/upgrade/uninstall smoke test in a Windows VM.
- full `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality.ps1`

Dependencies: commit 4.

### 6. `docs: update agent local operations and installer docs`

Files:

- `docs/installation/agent-configurator-gui.md`
- `docs/installation/agent-installation-and-operations.md`
- `docs/installation/installer-build.md`
- `docs/installation/windows-service.md`

Objective: document Tray, local status, service operations, installer contents, and security boundaries.

Risk: low, but documentation must not overpromise behavior until functional blocks are merged.

Required tests:

- docs review for sensitive terms and operational accuracy.
- full quality gate if docs are included in analyzer/format verification.

Dependencies: commits 1, 3, 4, and 5.

## Recommended Order

1. Local sanitized status service and Worker integration.
2. Certificate inventory classification and contract tests.
3. Configurator local operations.
4. Tray Monitor project.
5. Installer packaging for Tray and shortcuts.
6. Documentation updates.

This order keeps shared infrastructure and sensitive-data boundaries reviewable before UI and installer surfaces consume them.

## Files To Discard

No file is clearly generated or temporary in the pending set. No discard is recommended yet.

Watch items before committing functional blocks:

- `installer/wix/Package.wxs`: verify shortcut scope and uninstall behavior.
- `scripts/build-installer.ps1`: installer build may generate artifacts under `artifacts/`; those must remain untracked.
- Line ending warnings appeared for several text files because local Git is configured to rewrite LF as CRLF on checkout. Avoid committing purely line-ending churn unless a formatter requires it.

## Rodada 1 - Local Status

Execution date: 2026-05-15

Commit target: `feat: add sanitized local agent status`

### Files selected for the commit

- `src/Mws.Manifestador.Agent.Infrastructure/LocalStatus/AgentLocalStatusService.cs`
- `src/Mws.Manifestador.Agent.Infrastructure/LocalStatus/AgentLocalStatusSnapshot.cs`
- `src/Mws.Manifestador.Agent.Infrastructure/LocalStatus/AgentLocalStatusUpdate.cs`
- `src/Mws.Manifestador.Agent.Infrastructure/LocalStatus/AgentServiceState.cs`
- `src/Mws.Manifestador.Agent.Infrastructure/InfrastructureServiceCollectionExtensions.cs`
- `src/Mws.Manifestador.Agent.Infrastructure/Mws.Manifestador.Agent.Infrastructure.csproj`
- `src/Mws.Manifestador.Agent.Worker/Services/AgentWorker.cs`
- `tests/Mws.Manifestador.Agent.Tests/Infrastructure/AgentLocalStatusServiceTests.cs`
- `docs/WORKTREE_INVENTORY.md`

### Behavior implemented

- Adds a local status service that stores a simple JSON status file under `%ProgramData%\MWS Manifestador Agent\status.json`.
- Persists only operational status fields: agent id, installation id, API base URL, activation state, heartbeat/poll timestamps, version, service state, and sanitized last error message.
- Sanitizes error messages containing sensitive terms such as secret, password, token, PIN, private key, and PFX before writing status.
- Uses a temporary file plus replace move for status writes to reduce the chance of partial files.
- Reads local API configuration and DPAPI credential-file presence without exposing credential contents.
- Integrates the Worker minimally so startup, activation, heartbeat, polling, and operational errors update the status file.
- Catches local status write failures in the Worker and logs a warning instead of letting status persistence take down the polling loop.

### Scope decisions

- Tray Monitor is explicitly left out of this commit.
- Configurator UI/operations are explicitly left out of this commit.
- Installer and WiX changes are explicitly left out of this commit.
- Certificate classification/listing, SEFAZ, XML, SOAP, fixtures, and certificate tests are explicitly left out of this commit.
- Broad installation documentation is left for a later docs block.

### Commands executed

| Command | Result |
| --- | --- |
| `git status -sb` | Confirmed branch `codex/agent-worktree-inventory` with pending local functional blocks. |
| `git diff --name-status` | Confirmed broader dirty worktree and selected only LocalStatus/Worker/test/inventory files for this round. |
| `git diff -- src/Mws.Manifestador.Agent.Infrastructure/LocalStatus src/Mws.Manifestador.Agent.Worker/Services/AgentWorker.cs tests/Mws.Manifestador.Agent.Tests/Infrastructure/AgentLocalStatusServiceTests.cs` | Reviewed the LocalStatus block before staging. |
| `dotnet test Mws.Manifestador.Agent.sln --configuration Release --filter FullyQualifiedName~AgentLocalStatusServiceTests` | Passed: 3 tests, 0 failures, 0 skipped. |

### Next recommended block

Next prompt: create `feat: enhance windows certificate inventory classification`, limited to certificate summary/validator/list command/provider, the certificate fixture, and directly related tests.

## Rodada 2 - Certificate Inventory Classification

Execution date: 2026-05-15

Commit target: `feat: enhance windows certificate inventory classification`

### Files selected for the commit

- `src/Mws.Manifestador.Agent.Application/Certificates/CertificateSummary.cs`
- `src/Mws.Manifestador.Agent.Application/Certificates/CertificateValidator.cs`
- `src/Mws.Manifestador.Agent.Application/Commands/ListCertificatesCommandHandler.cs`
- `src/Mws.Manifestador.Agent.Application/Commands/ListedCertificate.cs`
- `src/Mws.Manifestador.Agent.Sefaz/Certificates/WindowsCertificateProvider.cs`
- `tests/Fixtures/list-certificates-result.json`
- `tests/Mws.Manifestador.Agent.Tests/Application/AgentDiagnosticsCommandHandlerTests.cs`
- `tests/Mws.Manifestador.Agent.Tests/Application/ListCertificatesCommandHandlerTests.cs`
- `tests/Mws.Manifestador.Agent.Tests/Application/TestCertificateCommandHandlerTests.cs`
- `tests/Mws.Manifestador.Agent.Tests/Certificates/CertificateValidatorTests.cs`
- `tests/Mws.Manifestador.Agent.Tests/Sefaz/TestSefazConnectivityCommandHandlerTests.cs`
- `tests/Mws.Manifestador.Agent.Tests/Sefaz/WindowsCertificateProviderClassificationTests.cs`
- `docs/WORKTREE_INVENTORY.md`

### Behavior implemented

- Extends certificate inventory summaries and listed-certificate payloads with safe diagnostic fields: common name, document, document type, store name/location, validity dates, private-key presence flag, ICP-Brasil flag, client-auth compatibility, CA flag, fiscal-candidate flag, classification, rejection reasons, and warnings.
- Classifies Windows Store certificates deterministically as `fiscal_candidate`, `expired_fiscal`, `missing_private_key`, `ca_certificate`, `system_certificate`, or `unknown`.
- Extracts CPF/CNPJ-like documents from certificate subject/extension text without exporting private material.
- Uses ICP-Brasil OID prefix and known issuer keywords as classification signals.
- Uses EKU/KU checks to reject certificates that are not suitable for client authentication/signature use.
- Filters `list-certificates` output to fiscal candidates by default, while supporting `include_rejected` and `include_expired` diagnostics flags.
- Keeps payload free from certificate private material, PIN, password, PFX/P12/PEM contents, HMAC secret, activation code, and fiscal XML.

### Scope decisions

- `WindowsCertificateProvider` lives under the Sefaz project, but only certificate inventory/classification code was included. No XML, SOAP, distribution, manifestation, or SEFAZ transport logic was changed.
- Tray Monitor, Configurator, installer/WiX, LocalStatus, and broad installation documentation were left out.
- Certificate tests use synthetic in-memory certificates and sanitized fixtures only.

### Commands executed

| Command | Result |
| --- | --- |
| `git status -sb` | Confirmed branch `codex/agent-worktree-inventory` with pending non-certificate blocks still dirty. |
| `git diff --name-status` | Confirmed broader dirty worktree and selected only certificate/listing/test/inventory files for this round. |
| `git diff -- src/Mws.Manifestador.Agent.Application/Certificates src/Mws.Manifestador.Agent.Application/Commands/ListCertificatesCommandHandler.cs src/Mws.Manifestador.Agent.Application/Commands/ListedCertificate.cs src/Mws.Manifestador.Agent.Domain/Certificates src/Mws.Manifestador.Agent.Infrastructure/Certificates src/Mws.Manifestador.Agent.Sefaz/Certificates tests` | Reviewed the certificate block before staging. |
| `dotnet test Mws.Manifestador.Agent.sln --configuration Release --filter FullyQualifiedName~Certificate` | Passed: 29 tests, 0 failures, 0 skipped. |
| `dotnet test Mws.Manifestador.Agent.sln --configuration Release --filter FullyQualifiedName~ListCertificates` | Passed: 6 tests, 0 failures, 0 skipped. |

### Next recommended block

Next prompt: create `feat: improve agent configurator local operations`, limited to the Configurator XAML/code-behind changes that consume the LocalStatus service. Keep Tray and installer packaging out of that commit.

## Rodada 3 - Configurator Local Operations

Execution date: 2026-05-15

Commit target: `feat: improve agent configurator local operations`

### Files selected for the commit

- `src/Mws.Manifestador.Agent.Configurator/MainWindow.xaml`
- `src/Mws.Manifestador.Agent.Configurator/MainWindow.xaml.cs`
- `docs/WORKTREE_INVENTORY.md`

### Behavior implemented

- Expands the Configurator window to display local operational status from the already committed `AgentLocalStatusService`.
- Splits UI feedback into local status and operation status so activation/service actions do not overwrite diagnostic context.
- Adds local operation buttons for service restart, service start, log-folder opening, and status refresh.
- Reuses `AgentLocalStatusService` for ProgramData paths, local status reads, service control, and log directory opening.
- Writes sanitized activation status after activation and clears the activation-code field after use.
- Keeps persisted local configuration free of activation code by writing `ActivationCode = null`.
- Shows administrator-permission guidance for service-control failures.

### Scope decisions

- Tray Monitor remains out of this commit.
- Installer/WiX and build script changes remain out of this commit.
- Broad installation documentation remains out of this commit.
- Certificate classification/listing, LocalStatus infrastructure, Worker, SEFAZ, XML, and SOAP are unchanged in this round.
- No real URL, HMAC secret, activation code, PIN, password, certificate payload, or XML fiscal data was added.

### Commands executed

| Command | Result |
| --- | --- |
| `git status -sb` | Confirmed branch `codex/agent-worktree-inventory` with pending Tray, installer/WiX, and docs blocks still dirty. |
| `git diff --name-status` | Confirmed only Configurator, Tray, installer/WiX, solution, and docs blocks remain pending before this round. |
| `git diff -- src/Mws.Manifestador.Agent.Configurator/MainWindow.xaml src/Mws.Manifestador.Agent.Configurator/MainWindow.xaml.cs` | Reviewed the Configurator diff before staging. |
| `rg -n -i "secret|token|password|senha|pin|private|pfx|pem|xml|activation|codigo|código|http://|https://" src/Mws.Manifestador.Agent.Configurator/MainWindow.xaml src/Mws.Manifestador.Agent.Configurator/MainWindow.xaml.cs` | Found only expected placeholder URL, activation-code labels/configuration keys, and security guidance text. No secret value found. |

### Next recommended block

Next prompt: create `feat: add agent tray monitor`, limited to `Mws.Manifestador.Agent.sln`, `src/Mws.Manifestador.Agent.Tray/*`, and inventory update. Keep installer/WiX and docs broad changes out of that commit.

## Rodada 4 - Tray Monitor

Execution date: 2026-05-15

Commit target: `feat: add agent tray monitor`

### Files selected for the commit

- `Mws.Manifestador.Agent.sln`
- `src/Mws.Manifestador.Agent.Tray/Mws.Manifestador.Agent.Tray.csproj`
- `src/Mws.Manifestador.Agent.Tray/Program.cs`
- `src/Mws.Manifestador.Agent.Tray/TrayApplicationContext.cs`
- `src/Mws.Manifestador.Agent.Tray/TrayResources.cs`
- `docs/WORKTREE_INVENTORY.md`

### Behavior implemented

- Adds a lightweight WinForms tray process that uses `NotifyIcon`.
- Reads only sanitized local status through the already committed `AgentLocalStatusService`.
- Shows service/activation state in the tray context menu and icon tooltip.
- Provides local actions to open the Configurator, start/restart/stop the service, open the logs directory, copy basic diagnostics, and exit the monitor.
- Refreshes status on a timer and when the menu opens.
- Handles missing/unavailable status or service-control failures with clear user-facing messages instead of crashing.
- Does not require network access to start.

### Scope decisions

- Installer/WiX and build script changes remain out of this commit.
- Broad installation documentation remains out of this commit.
- Configurator, Worker, LocalStatus infrastructure, certificate classification/listing, SEFAZ, XML, and SOAP are unchanged in this round.
- Generated `bin/` and `obj/` artifacts under the untracked Tray project directory were removed before staging and were not committed.
- No HMAC secret, activation code, password, PIN, private key, PFX/P12/PEM material, token, or fiscal XML was added.

### Commands executed

| Command | Result |
| --- | --- |
| `git status -sb` | Confirmed branch `codex/agent-worktree-inventory` with remaining Tray, installer/WiX, and docs blocks. |
| `git diff --name-status` | Confirmed tracked pending files before selecting the Tray block. |
| `git diff -- Mws.Manifestador.Agent.sln src/Mws.Manifestador.Agent.Tray` | Reviewed solution and Tray project scope before staging. |
| `rg -n "secret|token|password|senha|pin|private key|pfx|p12|pem|xml" src/Mws.Manifestador.Agent.Tray` | No matches found in Tray source files. |
| `Remove-Item` scoped to `src/Mws.Manifestador.Agent.Tray\bin` and `obj` | Removed generated build artifacts from the untracked Tray project directory. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality.ps1` | Initially failed inside the Tray block on CA1303 for a literal status-unavailable message and line-ending format. |
| `dotnet format Mws.Manifestador.Agent.sln --include ...Tray... docs/WORKTREE_INVENTORY.md` | Passed and normalized formatting for the Tray/inventory files. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality.ps1` | Passed after the Tray resource/format fix: build Release 0 warnings, 0 errors; tests 85 passed, 0 failed, 0 skipped. |

### Next recommended block

Next prompt: create `feat: package tray monitor in windows installer`, limited to `installer/wix/Package.wxs`, `scripts/build-installer.ps1`, and inventory update. Keep broad docs in the final docs commit.
