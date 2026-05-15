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
