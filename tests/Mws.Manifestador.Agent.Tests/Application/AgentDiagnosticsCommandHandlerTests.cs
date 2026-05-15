using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Mws.Manifestador.Agent.Application.Certificates;
using Mws.Manifestador.Agent.Application.Commands;
using Mws.Manifestador.Agent.Application.Configuration;
using Mws.Manifestador.Agent.Application.Diagnostics;
using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Application.Interfaces;
using Mws.Manifestador.Agent.Domain.Entities;
using Mws.Manifestador.Agent.Domain.Enums;

namespace Mws.Manifestador.Agent.Tests.Application;

public sealed class AgentDiagnosticsCommandHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    [Fact]
    public async Task ExecuteAsyncReturnsSanitizedPayload()
    {
        AgentDiagnosticsCommandHandler handler = CreateHandler(new FakeCertificateProvider([
            CreateSummary("ABC123"),
        ]));

        CommandExecutionOutcome outcome = await handler.ExecuteAsync(CreateCommand(), CancellationToken.None);

        outcome.Succeeded.Should().BeTrue();
        JsonNode node = SerializeResult(outcome);
        node["agent"]?["version"]?.GetValue<string>().Should().Be("1.2.3");
        node["agent"]?["machine_name"]?.GetValue<string>().Should().Be("MWS-CLIENTE");
        node["api"]?["base_url"]?.GetValue<string>().Should().Be("https://api.example.com");
        node["certificates"]?["inventory_count"]?.GetValue<int>().Should().Be(1);

        string json = node.ToJsonString(JsonOptions);
        json.Should().NotContain("activation");
        json.Should().NotContain("secret");
        json.Should().NotContain("pin");
        json.Should().NotContain("token");
        json.Should().NotContain("private_key");
    }

    [Fact]
    public async Task ExecuteAsyncWorksWhenCertificateStoreIsEmpty()
    {
        AgentDiagnosticsCommandHandler handler = CreateHandler(new FakeCertificateProvider([]));

        CommandExecutionOutcome outcome = await handler.ExecuteAsync(CreateCommand(), CancellationToken.None);

        JsonNode node = SerializeResult(outcome);
        node["certificates"]?["inventory_count"]?.GetValue<int>().Should().Be(0);
        node["certificates"]?["store_access_status"]?.GetValue<string>().Should().Be("accessible");
    }

    [Fact]
    public async Task ExecuteAsyncReportsStoreAccessFailureWithoutThrowing()
    {
        AgentDiagnosticsCommandHandler handler = CreateHandler(new FailingCertificateProvider());

        CommandExecutionOutcome outcome = await handler.ExecuteAsync(CreateCommand(), CancellationToken.None);

        outcome.Succeeded.Should().BeTrue();
        JsonNode node = SerializeResult(outcome);
        node["certificates"]?["inventory_count"]?.GetValue<int>().Should().Be(0);
        node["certificates"]?["store_access_status"]?.GetValue<string>().Should().Be("failed");
        node["certificates"]?["store_access_error_code"]?.GetValue<string>().Should().Be("CERTIFICATE_STORE_INVALID_OPERATION");
    }

    private static AgentDiagnosticsCommandHandler CreateHandler(ICertificateProvider certificateProvider)
    {
        AgentDiagnosticsCollector collector = new(
            new FakeAgentEnvironment(),
            certificateProvider,
            Options.Create(new AgentApiOptions
            {
                BaseUrl = new Uri("https://user:password@api.example.com/tenant/path?secret=1"),
            }));

        return new AgentDiagnosticsCommandHandler(collector);
    }

    private static JsonNode SerializeResult(CommandExecutionOutcome outcome)
    {
        JsonNode? node = JsonSerializer.SerializeToNode(outcome.Result?.Result, JsonOptions);
        node.Should().NotBeNull();

        return node ?? throw new InvalidOperationException("Diagnostics result was not serialized.");
    }

    private static AgentCommand CreateCommand()
    {
        using JsonDocument payload = JsonDocument.Parse("{}");

        return new AgentCommand(
            Guid.NewGuid(),
            CommandType.AgentDiagnosticsRequested,
            1,
            payload.RootElement.Clone(),
            null,
            null,
            0,
            1);
    }

    private static CertificateSummary CreateSummary(string thumbprint)
    {
        return new CertificateSummary(
            CertificateReference.A3(thumbprint, CertificateStoreScope.CurrentUser, "12345678000195"),
            "CN=Empresa Teste:12345678000195",
            "CN=AC Teste",
            thumbprint,
            "SERIAL001",
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1),
            true,
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

    private sealed class FakeAgentEnvironment : IAgentEnvironment
    {
        public string InstallationId => "install-001";

        public string MachineName => "MWS-CLIENTE";

        public string Version => "1.2.3";
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
            throw new NotSupportedException("Not used by diagnostics tests.");
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
            throw new NotSupportedException("Not used by diagnostics tests.");
        }
    }
}
