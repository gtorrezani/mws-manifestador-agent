using System.Globalization;
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

public sealed class ListCertificatesCommandHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    [Fact]
    public async Task ExecuteAsyncReturnsStructuredCertificateList()
    {
        ListCertificatesCommandHandler handler = new(new FakeCertificateProvider([
            CreateSummary("ABC123456789", DateTimeOffset.Parse("2025-01-01T00:00:00Z", CultureInfo.InvariantCulture), DateTimeOffset.Parse("2030-01-01T00:00:00Z", CultureInfo.InvariantCulture), true, CertificateStoreScope.CurrentUser, "12345678000195"),
            CreateSummary("DEF987654321", DateTimeOffset.Parse("2023-01-01T00:00:00Z", CultureInfo.InvariantCulture), DateTimeOffset.Parse("2024-01-01T00:00:00Z", CultureInfo.InvariantCulture), false, CertificateStoreScope.LocalMachine, "98765432000110", "CN=Certificado Vencido:98765432000110", "SERIAL002"),
        ]));

        CommandExecutionOutcome outcome = await handler.ExecuteAsync(CreateCommand(), CancellationToken.None);

        outcome.Succeeded.Should().BeTrue();

        JsonNode? actual = JsonSerializer.SerializeToNode(outcome.Result?.Result, JsonOptions);
        JsonNode? expected = JsonNode.Parse(await File.ReadAllTextAsync(FixturePath()));

        JsonNode.DeepEquals(actual, expected).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsyncMarksExpiredCertificatesAsInvalid()
    {
        ListCertificatesCommandHandler handler = new(new FakeCertificateProvider([
            CreateSummary("ABC", DateTimeOffset.UtcNow.AddYears(-2), DateTimeOffset.UtcNow.AddDays(-1), true, CertificateStoreScope.CurrentUser, null),
        ]));

        CommandExecutionOutcome outcome = await handler.ExecuteAsync(CreateCommand(), CancellationToken.None);

        ListedCertificate certificate = ExtractCertificates(outcome).Single();
        certificate.IsExpired.Should().BeTrue();
        certificate.IsValid.Should().BeFalse();
        certificate.ValidationMessage.Should().Be("Certificate is expired.");
    }

    [Fact]
    public async Task ExecuteAsyncKeepsCertificatesWithoutPrivateKeyInList()
    {
        ListCertificatesCommandHandler handler = new(new FakeCertificateProvider([
            CreateSummary("ABC", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1), false, CertificateStoreScope.CurrentUser, null),
        ]));

        CommandExecutionOutcome outcome = await handler.ExecuteAsync(CreateCommand(), CancellationToken.None);

        ListedCertificate certificate = ExtractCertificates(outcome).Single();
        certificate.HasPrivateKey.Should().BeFalse();
        certificate.IsValid.Should().BeFalse();
        certificate.ValidationMessage.Should().Be("Certificate does not have a private key.");
    }

    [Fact]
    public async Task ExecuteAsyncConvertsProviderFailureToStructuredFailure()
    {
        ListCertificatesCommandHandler handler = new(new FailingCertificateProvider());

        CommandExecutionOutcome outcome = await handler.ExecuteAsync(CreateCommand(), CancellationToken.None);

        outcome.Succeeded.Should().BeFalse();
        outcome.Failure?.ErrorCode.Should().Be("CERTIFICATE_STORE_LIST_FAILED");
    }

    private static AgentCommand CreateCommand()
    {
        using JsonDocument payload = JsonDocument.Parse("{}");

        return new AgentCommand(
            Guid.NewGuid(),
            CommandType.ListCertificates,
            100,
            payload.RootElement.Clone(),
            null,
            null,
            0,
            3);
    }

    private static CertificateSummary CreateSummary(
        string thumbprint,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter,
        bool hasPrivateKey,
        CertificateStoreScope storeScope,
        string? cnpj,
        string subject = "CN=Empresa Teste:12345678000195",
        string serialNumber = "SERIAL001")
    {
        return new CertificateSummary(
            CertificateReference.A3(thumbprint, storeScope, cnpj),
            subject,
            "CN=AC Teste",
            thumbprint,
            serialNumber,
            notBefore,
            notAfter,
            hasPrivateKey,
            cnpj,
            storeScope);
    }

    private static IReadOnlyCollection<ListedCertificate> ExtractCertificates(CommandExecutionOutcome outcome)
    {
        JsonNode? node = JsonSerializer.SerializeToNode(outcome.Result?.Result, JsonOptions);
        string json = node?["certificates"]?.ToJsonString() ?? "[]";

        return JsonSerializer.Deserialize<IReadOnlyCollection<ListedCertificate>>(json, JsonOptions) ?? [];
    }

    private static string FixturePath()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Fixtures", "list-certificates-result.json"));
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
            throw new NotSupportedException("Not used by list certificates tests.");
        }
    }

    private sealed class FailingCertificateProvider : ICertificateProvider
    {
        public Task<IReadOnlyCollection<CertificateSummary>> ListAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Store unavailable.");
        }

        public Task<X509Certificate2> GetCertificateAsync(CertificateReference reference, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Not used by list certificates tests.");
        }
    }
}
