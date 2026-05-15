using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FluentAssertions;
using Mws.Manifestador.Agent.Application.Certificates;
using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Application.Interfaces;
using Mws.Manifestador.Agent.Domain.Entities;
using Mws.Manifestador.Agent.Domain.Enums;
using Mws.Manifestador.Agent.Sefaz.Connectivity;
using Mws.Manifestador.Agent.Sefaz.Endpoints;
using Mws.Manifestador.Agent.Sefaz.Models;

namespace Mws.Manifestador.Agent.Tests.Sefaz;

public sealed class TestSefazConnectivityCommandHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    [Fact]
    public async Task ConfigurationOnlyReturnsSuccessPayload()
    {
        CertificateSummary summary = FixtureSummary();
        TestSefazConnectivityCommandHandler handler = CreateHandler([summary], endpointConfigured: true);

        CommandExecutionOutcome outcome = await handler.ExecuteAsync(CreateCommand("configuration_only", "homologation"), CancellationToken.None);

        outcome.Succeeded.Should().BeTrue();

        JsonNode? actual = JsonSerializer.SerializeToNode(outcome.Result?.Result, JsonOptions);
        JsonNode? expected = JsonNode.Parse(await File.ReadAllTextAsync(FixturePath()));
        actual!["duration_ms"] = 0;

        JsonNode.DeepEquals(actual, expected).Should().BeTrue();
    }

    [Fact]
    public async Task CertificateNotFoundReturnsStructuredFailure()
    {
        TestSefazConnectivityCommandHandler handler = CreateHandler([], endpointConfigured: true);

        CommandExecutionOutcome outcome = await handler.ExecuteAsync(CreateCommand("configuration_only", "homologation"), CancellationToken.None);

        outcome.Succeeded.Should().BeFalse();
        outcome.Failure?.ErrorCode.Should().Be("SEFAZ_CONNECTIVITY_CERTIFICATE_NOT_FOUND");
    }

    [Fact]
    public async Task InvalidCertificateReturnsStructuredFailure()
    {
        CertificateSummary summary = CreateSummary("ABC123456789", DateTimeOffset.UtcNow.AddYears(-2), DateTimeOffset.UtcNow.AddDays(-1), true);
        TestSefazConnectivityCommandHandler handler = CreateHandler([summary], endpointConfigured: true);

        CommandExecutionOutcome outcome = await handler.ExecuteAsync(CreateCommand("configuration_only", "homologation"), CancellationToken.None);

        outcome.Succeeded.Should().BeFalse();
        outcome.Failure?.ErrorCode.Should().Be("SEFAZ_CONNECTIVITY_CERTIFICATE_INVALID");
    }

    [Fact]
    public async Task EndpointNotConfiguredReturnsStructuredFailure()
    {
        CertificateSummary summary = FixtureSummary();
        TestSefazConnectivityCommandHandler handler = CreateHandler([summary], endpointConfigured: false);

        CommandExecutionOutcome outcome = await handler.ExecuteAsync(CreateCommand("configuration_only", "homologation"), CancellationToken.None);

        outcome.Succeeded.Should().BeFalse();
        outcome.Failure?.ErrorCode.Should().Be("SEFAZ_ENDPOINT_NOT_CONFIGURED");
    }

    [Fact]
    public async Task LiveHomologationInProductionReturnsNotConfiguredFailure()
    {
        CertificateSummary summary = FixtureSummary();
        TestSefazConnectivityCommandHandler handler = CreateHandler([summary], endpointConfigured: true);

        CommandExecutionOutcome outcome = await handler.ExecuteAsync(CreateCommand("live_homologation", "production"), CancellationToken.None);

        outcome.Succeeded.Should().BeFalse();
        outcome.Failure?.ErrorCode.Should().Be("SEFAZ_LIVE_TEST_NOT_CONFIGURED");
    }

    [Fact]
    public async Task LiveHomologationReturnsNotConfiguredFailure()
    {
        CertificateSummary summary = FixtureSummary();
        TestSefazConnectivityCommandHandler handler = CreateHandler([summary], endpointConfigured: true);

        CommandExecutionOutcome outcome = await handler.ExecuteAsync(CreateCommand("live_homologation", "homologation"), CancellationToken.None);

        outcome.Succeeded.Should().BeFalse();
        outcome.Failure?.ErrorCode.Should().Be("SEFAZ_LIVE_TEST_NOT_CONFIGURED");
    }

    private static TestSefazConnectivityCommandHandler CreateHandler(
        IReadOnlyCollection<CertificateSummary> certificates,
        bool endpointConfigured)
    {
        FakeCertificateProvider provider = new(certificates);

        return new TestSefazConnectivityCommandHandler(
            provider,
            new CertificateValidator(provider),
            new FakeEndpointResolver(endpointConfigured));
    }

    private static AgentCommand CreateCommand(string mode, string environment)
    {
        using JsonDocument payload = JsonDocument.Parse($$"""
        {
          "mode": "{{mode}}",
          "company_certificate_uuid": "11111111-1111-1111-1111-111111111111",
          "cnpj": "12345678000195",
          "uf": "SP",
          "environment": "{{environment}}",
          "thumbprint": "ABC123456789",
          "store_location": "CurrentUser",
          "correlation_id": "22222222-2222-2222-2222-222222222222"
        }
        """);

        return new AgentCommand(
            Guid.NewGuid(),
            CommandType.TestSefazConnectivity,
            100,
            payload.RootElement.Clone(),
            null,
            null,
            0,
            1);
    }

    private static CertificateSummary FixtureSummary()
    {
        return CreateSummary(
            "ABC123456789",
            DateTimeOffset.Parse("2025-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            DateTimeOffset.Parse("2030-01-01T00:00:00Z", CultureInfo.InvariantCulture),
            true);
    }

    private static CertificateSummary CreateSummary(
        string thumbprint,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter,
        bool hasPrivateKey)
    {
        return new CertificateSummary(
            CertificateReference.A3(thumbprint, CertificateStoreScope.CurrentUser, "12345678000195"),
            "CN=Empresa Teste:12345678000195",
            "CN=AC Teste",
            thumbprint,
            "SERIAL001",
            notBefore,
            notAfter,
            hasPrivateKey,
            "12345678000195",
            CertificateStoreScope.CurrentUser);
    }

    private static string FixturePath()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Fixtures", "test-sefaz-connectivity-result.json"));
    }

    private sealed class FakeEndpointResolver : ISefazEndpointResolver
    {
        private readonly bool endpointConfigured;

        public FakeEndpointResolver(bool endpointConfigured)
        {
            this.endpointConfigured = endpointConfigured;
        }

        public SefazEndpoint Resolve(SefazService service, SefazEnvironment environment, SefazUf uf)
        {
            if (!endpointConfigured)
            {
                throw new InvalidOperationException("No endpoint configured.");
            }

            return new SefazEndpoint(
                service,
                environment,
                SefazUf.AN,
                new Uri("https://hom1.nfe.fazenda.gov.br/NFeDistribuicaoDFe/NFeDistribuicaoDFe.asmx"),
                "soap-action",
                "operation",
                "namespace");
        }
    }

    private sealed class FakeCertificateProvider : ICertificateProvider
    {
        private readonly IReadOnlyCollection<CertificateSummary> certificates;

        public FakeCertificateProvider(IReadOnlyCollection<CertificateSummary> certificates)
        {
            this.certificates = certificates;
        }

        public Task<IReadOnlyCollection<CertificateSummary>> ListAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(certificates);
        }

        public Task<X509Certificate2> GetCertificateAsync(CertificateReference reference, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using RSA rsa = RSA.Create(2048);
            CertificateRequest request = new("CN=Fake", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            return Task.FromResult(request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1)));
        }
    }
}
