# MWS Manifestador Agent - SEFAZ Layer

## Scope

The SEFAZ layer consumes NF-e services directly through SOAP/XML, without ACBr:

- `NFeDistribuicaoDFe` for distribution of fiscal documents of interest.
- `NFeRecepcaoEvento` for recipient manifestation events.

## Official References

- Portal Nacional NF-e Web Services: https://www.nfe.fazenda.gov.br/portal/webServices.aspx
- Homologation Web Services: https://hom.nfe.fazenda.gov.br/portal/webServices.aspx
- MOC and schemas index: https://moc.sped.fazenda.pr.gov.br/

## Components

- `DistributionXmlBuilder`: builds `distDFeInt` v1.01 requests using `distNSU`, `consNSU` or `consChNFe`.
- `ManifestationXmlBuilder`: builds `envEvento` v1.00 batches for events `210210`, `210200`, `210220` and `210240`.
- `XmlSigner`: signs the `infEvento` element using XMLDSig and the selected X509 certificate private key.
- `NfeXmlSchemaValidator`: validates XML against official XSD files from `Sefaz:SchemaDirectory`.
- `SoapEnvelopeBuilder`: wraps NF-e payloads in SOAP 1.2 envelopes.
- `SefazSoapTransport`: posts SOAP requests and supports a client X509 certificate.
- `DistributionResponseParser`: parses `retDistDFeInt`, handles `docZip`, Base64 and GZip.
- `FiscalDocumentParser`: parses `resNFe` and full `NFe/infNFe`.
- `EventResponseParser`: parses `retEnvEvento`, event status, reason and protocol.
- `SefazEndpointResolver`: resolves service endpoints by UF and environment.
- `SanitizedXmlDiagnostics`: logs only root/status/correlation id when diagnostic XML logging is enabled.

## Schemas

The agent does not embed official XSD files in source control. Install the official NF-e schema package and configure:

```json
{
  "Sefaz": {
    "SchemaDirectory": "C:\\ProgramData\\MWS Manifestador Agent\\schemas\\nfe"
  }
}
```

Expected names include:

- `distDFeInt_v1.01.xsd`
- `envEvento_v1.00.xsd`

If a schema is missing, the layer returns a technical failure through `Result<T>`; it does not skip validation silently.

## Command Payload Contract

Distribution:

```json
{
  "uf": "SP",
  "environment": "homologation",
  "cnpj": "12345678000195",
  "certificate_thumbprint": "THUMBPRINT",
  "last_nsu": "0",
  "correlation_id": "optional"
}
```

Manifestation:

```json
{
  "uf": "SP",
  "environment": "production",
  "cnpj": "12345678000195",
  "certificate_thumbprint": "THUMBPRINT",
  "access_key": "12345678901234567890123456789012345678901234",
  "sequence": 1,
  "justification": "Obrigatorio para 210240",
  "lot_id": "1"
}
```

## Rules Enforced

- NF-e access key must have 44 digits.
- CNPJ must have 14 digits.
- `210240` requires justification.
- `210210` is treated as non-conclusive by event code only; no automatic conclusive event is generated.
- Event batch size is limited by `Sefaz:EventBatchLimit` and defaults to `20`.
- XML request and response are stored through `ITemporaryXmlStorage`.
- Full XML is never logged by default.

## Technical TODOs Before Production Use

These items depend on real validation against official SEFAZ homologation endpoints with valid test certificates and official schema packages. The current automated tests use sanitized XML fixtures and local unit tests only; they do not prove end-to-end acceptance by SEFAZ.

- TODO: Validate `NFeDistribuicaoDFe` SOAP envelope, headers, TLS client certificate behavior and response parsing against the official homologation environment.
- TODO: Validate `NFeRecepcaoEvento` for events `210210`, `210200`, `210220` and `210240` in homologation, including signed `infEvento`, batch limits, protocol extraction and rejection handling.
- TODO: Confirm every endpoint URL in `SefazEndpointResolver` against the current official NF-e web services list before enabling production.
- TODO: Install the official NF-e XSD package in an operational environment and confirm schema validation with real request/response samples.
- TODO: Replace configuration-only connectivity diagnostics with a real SEFAZ HTTPS/SOAP probe before treating `test_sefaz_connectivity` as proof of network availability.
- TODO: Execute manual tests with A3 tokens from Windows Certificate Store to confirm provider PIN prompts, token removal behavior and private-key signing under the target customer environment.
