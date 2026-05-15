# Quality Baseline

Execution date: 2026-05-15 13:11:13 -03:00

## Environment notes

- Repository: `mws-manifestador-agent`
- Branch at execution start: `main`
- The worktree already contained unrelated local changes before this baseline was created.

## Commands executed

| Command | Result |
| --- | --- |
| `dotnet restore Mws.Manifestador.Agent.sln` | Passed. All projects were already up to date for restore. |
| `dotnet build Mws.Manifestador.Agent.sln --configuration Release --no-restore` | Passed. 0 warnings, 0 errors. |
| `dotnet format Mws.Manifestador.Agent.sln --verify-no-changes --no-restore` | Passed. |
| `dotnet test Mws.Manifestador.Agent.sln --configuration Release --no-build` | Passed. 75 tests passed, 0 failed, 0 skipped. |
| `dotnet test Mws.Manifestador.Agent.sln --configuration Release --no-build --filter FullyQualifiedName~HmacSignatureServiceTests` | Passed. 2 HMAC tests passed. |
| `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\quality.ps1` | Passed. Restore, Release build, format verification, and 75 tests completed successfully. |

## Failures found

- No command failures were observed in the agent repository during this baseline.

## Actions taken

- Verified the full Release quality path: restore, build, format check, and test.
- Verified the HMAC contract test fixture through the focused `HmacSignatureServiceTests` filter.
- Verified the local `scripts\quality.ps1` wrapper.

## Next technical risks

- Keep the HMAC fixture synchronized with the Laravel Web/API fixture whenever the authentication contract changes.
- Continue avoiding real fiscal XML, certificates, private keys, PFX/P12 files, and secrets in fixtures or logs.
- Add regression tests before changing SEFAZ transport, XML signing, Windows certificate access, credential storage, or command lifecycle behavior.
