# NF-e Schema Validation

## Purpose

The Agent validates NF-e XML against official XSD files before using a payload as technically valid. For the implemented distribution flow, `distDFeInt` is validated before the SOAP call and `retDistDFeInt` plus supported `docZip` XMLs are validated before the command result is returned to the Web/API.

## Runtime Configuration

```json
{
  "Sefaz": {
    "SchemaValidation": {
      "Enabled": true,
      "Strict": true,
      "SchemasPath": "Schemas/NFe",
      "ValidateOutgoing": true,
      "ValidateIncoming": true,
      "FailOnUnknownSchema": true
    }
  }
}
```

Recommended defaults:

- Development: `Enabled=true`, `Strict=false`, `FailOnUnknownSchema=false`.
- Production: `Enabled=true`, `Strict=true`, `FailOnUnknownSchema=true`.

## Included Official Schemas

Source indexes reviewed on 2026-05-15:

- `https://hom.nfe.fazenda.gov.br/portal/listaConteudo.aspx?tipoConteudo=BMPFMBoln3w%3D`
- `https://www.nfe.fazenda.gov.br/portal/listaConteudo.aspx?tipoConteudo=BMPFMBoln3w%3D`

Packages used:

- NF-e Distribuicao de DF-e v1.03, published by the NF-e Portal.
- NF-e/NFC-e PL 010b v1.30, published by the NF-e Portal.
- NF-e Evento Generico v1.01, published by the NF-e Portal.

Files included under `src/Mws.Manifestador.Agent.Sefaz/Schemas/NFe`:

- `DFeTiposBasicos_v1.00.xsd`
- `distDFeInt_v1.01.xsd`
- `envEvento_v1.00.xsd`
- `leiauteEvento_v1.00.xsd`
- `leiauteNFe_v4.00.xsd`
- `nfe_v4.00.xsd`
- `procEventoNFe_v1.00.xsd`
- `resEvento_v1.01.xsd`
- `resNFe_v1.01.xsd`
- `retDistDFeInt_v1.01.xsd`
- `retEnvEvento_v1.00.xsd`
- `tiposBasico_v1.03.xsd`
- `tiposBasico_v4.00.xsd`
- `tiposDistDFe_v1.01.xsd`
- `xmldsig-core-schema_v1.01.xsd`

## Behavior

- Malformed XML always fails.
- XML that does not match a known official schema always fails.
- Missing schema fails in strict mode.
- Unknown schema fails in strict mode or when `FailOnUnknownSchema=true`.
- Unknown `docZip` schema is preserved as raw XML when strict mode is disabled.
- The full XML is not logged by default; diagnostic logging must remain sanitized.

## Command Failure Contract

Schema failures return:

```json
{
  "error_code": "SEFAZ_XML_SCHEMA_INVALID",
  "error_message": "XML rejected by technical schema validation.",
  "error_details": {
    "correlation_id": "corr-id",
    "schema_name": "distDFeInt_v1.01.xsd",
    "root_element": "distDFeInt",
    "validation_status": "InvalidXml",
    "validation_errors": [
      {
        "message": "Sanitized validator message",
        "line_number": 1,
        "line_position": 2
      }
    ]
  }
}
```

The Web/API must treat this as a technical validation failure, not as a SEFAZ fiscal rejection. `last_nsu` must not advance.

## Updating Schemas

1. Download the current ZIP packages from the official NF-e Portal schema index.
2. Extract only official `.xsd` files required by implemented flows.
3. Replace files under `Schemas/NFe` without renaming them.
4. Run `dotnet test Mws.Manifestador.Agent.sln --configuration Release`.
5. Run a homologation `sync_fiscal_documents` test before enabling production.

## Known Limitations

- TODO: add `procNFe_v4.00.xsd` from an official package when available in the maintained package used by the project. Until then `nfeProc/procNFe` entries are preserved as raw XML in non-strict mode and fail explicitly in strict mode.
- TODO: validate `envEvento` and `retEnvEvento` with real manifestation homologation once event submission is implemented.
- TODO: re-check SOAP 1.1 versus SOAP 1.2 per endpoint during homologation hardening.
