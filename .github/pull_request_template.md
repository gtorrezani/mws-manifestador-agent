## Quality checklist

- [ ] Build passes with zero warnings: `dotnet build Mws.Manifestador.Agent.sln --configuration Release`.
- [ ] Formatting passes: `dotnet format Mws.Manifestador.Agent.sln --verify-no-changes`.
- [ ] Tests pass: `dotnet test Mws.Manifestador.Agent.sln --configuration Release`.
- [ ] Nullable warnings are not suppressed without a narrow justification.
- [ ] Fiscal states use enums/value objects, not string literals.
- [ ] Manifestation event code changes include tests.
- [ ] HMAC authentication, command idempotency, and command lock/concurrency tests are preserved or extended.
- [ ] No real fiscal XML, access keys, CNPJs, certificates, PFX/P12, private keys, PINs, or passwords were committed.
- [ ] Fixtures are sanitized and generated inline when possible.
- [ ] Logs do not include XML content, certificate secrets, PINs, passwords, HMAC secrets, or full sensitive payloads.
- [ ] Worker orchestration stays thin; fiscal logic remains in Application/Sefaz/Domain services.
