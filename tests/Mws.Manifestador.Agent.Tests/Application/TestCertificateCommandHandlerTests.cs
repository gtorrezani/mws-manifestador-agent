using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FluentAssertions;
using Mws.Manifestador.Agent.Application.Certificates;
using Mws.Manifestador.Agent.Application.Commands;
using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Application.Interfaces;
using Mws.Manifestador.Agent.Domain.Entities;
using Mws.Manifestador.Agent.Domain.Enums;

namespace Mws.Manifestador.Agent.Tests.Application;

public sealed class TestCertificateCommandHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    [Fact]
    public async Task ExecuteAsyncReturnsSuccessPayloadForValidCertificate()
    {
        CertificateSummary summary = FixtureSummary();
        TestCertificateCommandHandler handler = CreateHandler([summary], certificateAccessSucceeds: true);

        CommandExecutionOutcome outcome = await handler.ExecuteAsync(CreateCommand(summary.Thumbprint), CancellationToken.None);

        outcome.Succeeded.Should().BeTrue();

        JsonNode? actual = JsonSerializer.SerializeToNode(outcome.Result?.Result, JsonOptions);
        JsonNode? expected = JsonNode.Parse(await File.ReadAllTextAsync(FixturePath()));

        JsonNode.DeepEquals(actual, expected).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsyncReturnsExpiredFailureForExpiredCertificate()
    {
        CertificateSummary summary = CreateSummary("ABC", DateTimeOffset.UtcNow.AddYears(-2), DateTimeOffset.UtcNow.AddDays(-1), true);
        TestCertificateCommandHandler handler = CreateHandler([summary], certificateAccessSucceeds: true);

        CommandExecutionOutcome outcome = await handler.ExecuteAsync(CreateCommand(summary.Thumbprint), CancellationToken.None);

        outcome.Succeeded.Should().BeFalse();
        outcome.Failure?.ErrorCode.Should().Be("CERTIFICATE_EXPIRED");
    }

    [Fact]
    public async Task ExecuteAsyncReturnsWithoutPrivateKeyFailure()
    {
        CertificateSummary summary = CreateSummary("ABC", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1), false);
        TestCertificateCommandHandler handler = CreateHandler([summary], certificateAccessSucceeds: true);

        CommandExecutionOutcome outcome = await handler.ExecuteAsync(CreateCommand(summary.Thumbprint), CancellationToken.None);

        outcome.Succeeded.Should().BeFalse();
        outcome.Failure?.ErrorCode.Should().Be("CERTIFICATE_WITHOUT_PRIVATE_KEY");
    }

    [Fact]
    public async Task ExecuteAsyncReturnsNotFoundFailure()
    {
        TestCertificateCommandHandler handler = CreateHandler([], certificateAccessSucceeds: true);

        CommandExecutionOutcome outcome = await handler.ExecuteAsync(CreateCommand("NOTFOUND"), CancellationToken.None);

        outcome.Succeeded.Should().BeFalse();
        outcome.Failure?.ErrorCode.Should().Be("CERTIFICATE_NOT_FOUND");
    }

    [Fact]
    public async Task ExecuteAsyncConvertsCryptographicProviderErrorToStructuredFailure()
    {
        CertificateSummary summary = FixtureSummary();
        TestCertificateCommandHandler handler = CreateHandler(
            [summary],
            certificateAccessSucceeds: false,
            CertificateErrorCode.CertificateProviderAccessDenied);

        CommandExecutionOutcome outcome = await handler.ExecuteAsync(CreateCommand(summary.Thumbprint), CancellationToken.None);

        outcome.Succeeded.Should().BeFalse();
        outcome.Failure?.ErrorCode.Should().Be("CERTIFICATE_PRIVATE_KEY_INACCESSIBLE");
    }

    private static TestCertificateCommandHandler CreateHandler(
        IReadOnlyCollection<CertificateSummary> certificates,
        bool certificateAccessSucceeds,
        CertificateErrorCode errorCode = CertificateErrorCode.None)
    {
        FakeCertificateProvider provider = new(certificates, certificateAccessSucceeds, errorCode);

        return new TestCertificateCommandHandler(new CertificateValidator(provider), provider);
    }

    private static AgentCommand CreateCommand(string thumbprint)
    {
        using JsonDocument payload = JsonDocument.Parse($$"""
        {
          "thumbprint": "{{thumbprint}}",
          "store_location": "CurrentUser",
          "correlation_id": "11111111-1111-1111-1111-111111111111"
        }
        """);

        return new AgentCommand(
            Guid.NewGuid(),
            CommandType.TestCertificate,
            100,
            payload.RootElement.Clone(),
            null,
            null,
            0,
            3);
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
            CertificateStoreScope.CurrentUser,
            "Empresa Teste",
            "12345678000195",
            "cnpj",
            false,
            true,
            true,
            true,
            "fiscal_candidate",
            [],
            ["Tipo A1/A3 nao confirmado automaticamente."]);
    }

    private static string FixturePath()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Fixtures", "test-certificate-result.json"));
    }

    private sealed class FakeCertificateProvider : ICertificateProvider
    {
        private readonly bool certificateAccessSucceeds;
        private readonly IReadOnlyCollection<CertificateSummary> certificates;
        private readonly CertificateErrorCode errorCode;

        public FakeCertificateProvider(
            IReadOnlyCollection<CertificateSummary> certificates,
            bool certificateAccessSucceeds,
            CertificateErrorCode errorCode)
        {
            this.certificates = certificates;
            this.certificateAccessSucceeds = certificateAccessSucceeds;
            this.errorCode = errorCode;
        }

        public Task<IReadOnlyCollection<CertificateSummary>> ListAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(certificates);
        }

        public Task<X509Certificate2> GetCertificateAsync(CertificateReference reference, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!certificateAccessSucceeds)
            {
                throw new CertificateProviderException(errorCode, "Private key access failed.");
            }

            using RSA rsa = RSA.Create(2048);
            CertificateRequest request = new("CN=Fake", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            return Task.FromResult(request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1)));
        }
    }
}
