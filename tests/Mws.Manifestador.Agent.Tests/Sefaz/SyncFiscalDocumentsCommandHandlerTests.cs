using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mws.Manifestador.Agent.Application.Certificates;
using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Application.Interfaces;
using Mws.Manifestador.Agent.Domain.Entities;
using Mws.Manifestador.Agent.Domain.Enums;
using Mws.Manifestador.Agent.Sefaz;
using Mws.Manifestador.Agent.Sefaz.Configuration;
using Mws.Manifestador.Agent.Sefaz.Distribution;
using Mws.Manifestador.Agent.Sefaz.Endpoints;
using Mws.Manifestador.Agent.Sefaz.Models;
using Mws.Manifestador.Agent.Sefaz.Parsing;
using Mws.Manifestador.Agent.Sefaz.Soap;
using Mws.Manifestador.Agent.Sefaz.Validation;
using Mws.Manifestador.Agent.Sefaz.Xml;

namespace Mws.Manifestador.Agent.Tests.Sefaz;

public sealed class SyncFiscalDocumentsCommandHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [Fact]
    public async Task ExecuteAsyncReturnsSuccessForHomologationDistribution()
    {
        string responseXml = DistributionResponse("138", "Documento localizado", "000000000000011", DocZip(SummaryXml()));
        SyncFiscalDocumentsCommandHandler handler = CreateHandler(new FakeSoapTransport(responseXml));
        AgentCommand command = Command("""
            {
              "cnpj": "12345678000195",
              "uf": "SP",
              "environment": "homologation",
              "certificate_thumbprint": "ABC123",
              "store_location": "CurrentUser",
              "last_nsu": "000000000000010",
              "correlation_id": "corr-sync-1"
            }
            """);

        CommandExecutionOutcome outcome = await handler.ExecuteAsync(command, CancellationToken.None);

        outcome.Succeeded.Should().BeTrue();
        outcome.Result?.SefazStatusCode.Should().Be("138");
        JsonElement result = JsonSerializer.SerializeToElement(outcome.Result?.Result, JsonOptions);
        result.GetProperty("distribution_result").GetString().Should().Be("documents_found");
        result.GetProperty("documents").GetArrayLength().Should().Be(1);
        result.GetProperty("documents")[0].GetProperty("access_key").GetString().Should().Be("35260512345678000195550010000000011000000010");
    }

    [Fact]
    public async Task ExecuteAsyncReturnsSuccessForNoDocuments()
    {
        SyncFiscalDocumentsCommandHandler handler = CreateHandler(new FakeSoapTransport(DistributionResponse("137", "Nenhum documento localizado", "000000000000010", null)));

        CommandExecutionOutcome outcome = await handler.ExecuteAsync(Command(), CancellationToken.None);

        outcome.Succeeded.Should().BeTrue();
        outcome.Result?.SefazStatusCode.Should().Be("137");
        JsonElement result = JsonSerializer.SerializeToElement(outcome.Result?.Result, JsonOptions);
        result.GetProperty("distribution_result").GetString().Should().Be("no_documents");
    }

    [Fact]
    public async Task ExecuteAsyncFailsForConsumptionDenied()
    {
        SyncFiscalDocumentsCommandHandler handler = CreateHandler(new FakeSoapTransport(DistributionResponse("656", "Consumo indevido", "000000000000010", null)));

        CommandExecutionOutcome outcome = await handler.ExecuteAsync(Command(), CancellationToken.None);

        outcome.Succeeded.Should().BeFalse();
        outcome.Failure?.ErrorCode.Should().Be("SEFAZ_DISTRIBUTION_CONSUMPTION_DENIED");
        outcome.Failure?.SefazStatusCode.Should().Be("656");
        JsonElement details = JsonSerializer.SerializeToElement(outcome.Failure?.ErrorDetails, JsonOptions);
        details.GetProperty("distribution_result").GetString().Should().Be("consumption_denied");
        details.GetProperty("retry_after_hint_minutes").GetInt32().Should().Be(60);
    }

    [Fact]
    public async Task ExecuteAsyncBlocksProductionWithoutExplicitFlag()
    {
        SyncFiscalDocumentsCommandHandler handler = CreateHandler(new FakeSoapTransport(DistributionResponse("137", "Nenhum documento localizado", "0", null)));

        CommandExecutionOutcome outcome = await handler.ExecuteAsync(
            Command("""
                {
                  "cnpj": "12345678000195",
                  "uf": "SP",
                  "environment": "production",
                  "certificate_thumbprint": "ABC123",
                  "last_nsu": "000000000000000"
                }
                """),
            CancellationToken.None);

        outcome.Succeeded.Should().BeFalse();
        outcome.Failure?.ErrorCode.Should().Be("SEFAZ_DISTRIBUTION_PRODUCTION_BLOCKED");
    }

    [Fact]
    public async Task ExecuteAsyncFailsForInvalidDocZip()
    {
        string responseXml = """
            <retDistDFeInt xmlns="http://www.portalfiscal.inf.br/nfe" versao="1.01">
              <tpAmb>2</tpAmb><verAplic>AN_1.0</verAplic><cStat>138</cStat><xMotivo>Documento localizado</xMotivo>
              <dhResp>2026-05-14T10:15:00-03:00</dhResp><ultNSU>000000000000011</ultNSU><maxNSU>000000000000011</maxNSU>
              <loteDistDFeInt><docZip NSU="000000000000011" schema="resNFe_v1.01.xsd">invalid</docZip></loteDistDFeInt>
            </retDistDFeInt>
            """;
        SyncFiscalDocumentsCommandHandler handler = CreateHandler(new FakeSoapTransport(responseXml));

        CommandExecutionOutcome outcome = await handler.ExecuteAsync(Command(), CancellationToken.None);

        outcome.Succeeded.Should().BeFalse();
        outcome.Failure?.ErrorCode.Should().Be("SEFAZ_XML_SCHEMA_INVALID");
    }

    [Fact]
    public async Task ExecuteAsyncFailsWhenIncomingXmlDoesNotMatchOfficialSchema()
    {
        string responseXml = """
            <retDistDFeInt xmlns="http://www.portalfiscal.inf.br/nfe" versao="1.01">
              <cStat>137</cStat><xMotivo>Nenhum documento localizado</xMotivo>
              <ultNSU>000000000000010</ultNSU><maxNSU>000000000000010</maxNSU>
            </retDistDFeInt>
            """;
        SyncFiscalDocumentsCommandHandler handler = CreateHandler(new FakeSoapTransport(responseXml));

        CommandExecutionOutcome outcome = await handler.ExecuteAsync(Command(), CancellationToken.None);

        outcome.Succeeded.Should().BeFalse();
        outcome.Failure?.ErrorCode.Should().Be("SEFAZ_XML_SCHEMA_INVALID");
    }

    [Fact]
    public async Task ExecuteAsyncPreservesUnknownDocZipSchemaWhenStrictModeIsDisabled()
    {
        string responseXml = DistributionResponse("138", "Documento localizado", "000000000000011", DocZip(UnknownDocumentXml(), "unknownDoc_v1.00.xsd"));
        SyncFiscalDocumentsCommandHandler handler = CreateHandler(new FakeSoapTransport(responseXml));

        CommandExecutionOutcome outcome = await handler.ExecuteAsync(Command(), CancellationToken.None);

        outcome.Succeeded.Should().BeTrue();
        JsonElement result = JsonSerializer.SerializeToElement(outcome.Result?.Result, JsonOptions);
        result.GetProperty("documents")[0].GetProperty("raw_xml").GetString().Should().Contain("unknownDoc");
    }

    [Fact]
    public async Task ExecuteAsyncFailsForUnknownDocZipSchemaWhenStrictModeIsEnabled()
    {
        string responseXml = DistributionResponse("138", "Documento localizado", "000000000000011", DocZip(UnknownDocumentXml(), "unknownDoc_v1.00.xsd"));
        SyncFiscalDocumentsCommandHandler handler = CreateHandler(new FakeSoapTransport(responseXml), new SefazOptions
        {
            SchemaValidation = new SchemaValidationOptions
            {
                Enabled = true,
                Strict = true,
                SchemasPath = Path.Combine(AppContext.BaseDirectory, "Schemas", "NFe"),
                ValidateOutgoing = true,
                ValidateIncoming = true,
                FailOnUnknownSchema = true,
            },
        });

        CommandExecutionOutcome outcome = await handler.ExecuteAsync(Command(), CancellationToken.None);

        outcome.Succeeded.Should().BeFalse();
        outcome.Failure?.ErrorCode.Should().Be("SEFAZ_XML_SCHEMA_INVALID");
    }

    private static SyncFiscalDocumentsCommandHandler CreateHandler(ISefazSoapTransport transport, SefazOptions? options = null)
    {
        options ??= new SefazOptions();
        IOptions<SefazOptions> configuredOptions = Options.Create(options);

        return new SyncFiscalDocumentsCommandHandler(
            new CommandPayloadReader(),
            new FakeCertificateProvider(),
            new DistributionXmlBuilder(),
            new SoapEnvelopeBuilder(),
            transport,
            new DistributionResponseParser(new NfeDocumentDecompressor(), new FiscalDocumentParser()),
            new NfeXmlSchemaValidator(configuredOptions),
            new FakeEndpointResolver(),
            new FakeTemporaryXmlStorage(),
            new SanitizedXmlDiagnostics(configuredOptions, NullLogger<SanitizedXmlDiagnostics>.Instance),
            configuredOptions);
    }

    private static AgentCommand Command(string json = """
        {
          "cnpj": "12345678000195",
          "uf": "SP",
          "environment": "homologation",
          "certificate_thumbprint": "ABC123",
          "store_location": "CurrentUser",
          "last_nsu": "000000000000000",
          "correlation_id": "corr-sync-test"
        }
        """)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        return new AgentCommand(
            Guid.NewGuid(),
            CommandType.SyncFiscalDocuments,
            100,
            document.RootElement.Clone(),
            null,
            null,
            0,
            1);
    }

    private static string DistributionResponse(string statusCode, string reason, string nsu, string? docZip)
    {
        string lote = docZip is null ? string.Empty : $"<loteDistDFeInt>{docZip}</loteDistDFeInt>";

        return $"""
            <retDistDFeInt xmlns="http://www.portalfiscal.inf.br/nfe" versao="1.01">
              <tpAmb>2</tpAmb>
              <verAplic>AN_1.0</verAplic>
              <cStat>{statusCode}</cStat>
              <xMotivo>{reason}</xMotivo>
              <dhResp>2026-05-14T10:15:00-03:00</dhResp>
              <ultNSU>{nsu}</ultNSU>
              <maxNSU>{nsu}</maxNSU>
              {lote}
            </retDistDFeInt>
            """;
    }

    private static string DocZip(string xml, string schema = "resNFe_v1.01.xsd")
    {
        return $"<docZip NSU=\"000000000000011\" schema=\"{schema}\">{Compress(xml)}</docZip>";
    }

    private static string SummaryXml()
    {
        return """
            <resNFe xmlns="http://www.portalfiscal.inf.br/nfe" versao="1.01">
              <chNFe>35260512345678000195550010000000011000000010</chNFe>
              <CNPJ>12345678000195</CNPJ>
              <xNome>Emitente Homologacao</xNome>
              <IE>123456789012</IE>
              <dhEmi>2026-05-14T10:15:00-03:00</dhEmi>
              <tpNF>1</tpNF>
              <vNF>123.45</vNF>
              <dhRecbto>2026-05-14T10:20:00-03:00</dhRecbto>
              <nProt>135260000000001</nProt>
              <cSitNFe>1</cSitNFe>
            </resNFe>
            """;
    }

    private static string UnknownDocumentXml()
    {
        return """
            <unknownDoc xmlns="http://www.portalfiscal.inf.br/nfe" versao="1.00">
              <value>sanitized</value>
            </unknownDoc>
            """;
    }

    private static string Compress(string xml)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(xml);
        using MemoryStream output = new();
        using (GZipStream gzip = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(bytes);
        }

        return Convert.ToBase64String(output.ToArray());
    }

    private sealed class FakeSoapTransport(string responseXml) : ISefazSoapTransport
    {
        public Task<string> PostAsync(
            SefazEndpoint endpoint,
            string envelopeXml,
            X509Certificate2? clientCertificate,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            endpoint.Url.ToString().Should().Contain("NFeDistribuicaoDFe");
            envelopeXml.Should().Contain("nfeDistDFeInteresse");
            clientCertificate.Should().NotBeNull();

            return Task.FromResult(responseXml);
        }
    }

    private sealed class FakeCertificateProvider : ICertificateProvider
    {
        public Task<IReadOnlyCollection<CertificateSummary>> ListAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyCollection<CertificateSummary>>([]);
        }

        public Task<X509Certificate2> GetCertificateAsync(CertificateReference reference, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using RSA rsa = RSA.Create(2048);
            CertificateRequest request = new("CN=Test Certificate", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

            return Task.FromResult(certificate);
        }
    }

    private sealed class FakeEndpointResolver : ISefazEndpointResolver
    {
        public SefazEndpoint Resolve(SefazService service, SefazEnvironment environment, SefazUf uf)
        {
            return new SefazEndpoint(
                service,
                environment,
                SefazUf.AN,
                new Uri("https://hom1.nfe.fazenda.gov.br/NFeDistribuicaoDFe/NFeDistribuicaoDFe.asmx"),
                "http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe/nfeDistDFeInteresse",
                "nfeDistDFeInteresse",
                "http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe");
        }
    }

    private sealed class FakeTemporaryXmlStorage : ITemporaryXmlStorage
    {
        public Task<XmlArtifact> SaveAsync(string fileName, string xmlContent, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
#pragma warning disable CA1308
            string contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(xmlContent))).ToLowerInvariant();
#pragma warning restore CA1308

            return Task.FromResult(new XmlArtifact("memory", fileName, contentHash));
        }
    }
}
