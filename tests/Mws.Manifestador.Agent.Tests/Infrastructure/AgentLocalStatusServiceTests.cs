using System.Globalization;
using FluentAssertions;
using Mws.Manifestador.Agent.Application.Interfaces;
using Mws.Manifestador.Agent.Infrastructure.LocalStatus;

namespace Mws.Manifestador.Agent.Tests.Infrastructure;

public sealed class AgentLocalStatusServiceTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), "mws-agent-status-tests", Guid.NewGuid().ToString("N"));
    private readonly AgentLocalStatusService service;

    public AgentLocalStatusServiceTests()
    {
        service = new AgentLocalStatusService(new FakeAgentEnvironment(), tempDirectory, "MWSManifestadorAgent-Test-Does-Not-Exist");
    }

    public void Dispose()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task WriteStatusAsyncStoresSanitizedPayloadWithoutSensitiveTerms()
    {
        await service.WriteStatusAsync(
            new AgentLocalStatusUpdate
            {
                AgentId = "agent-001",
                ApiBaseUrl = new Uri("https://api.example.test"),
                Activated = true,
                LastHeartbeatAt = DateTimeOffset.Parse("2026-05-14T10:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
                LastErrorMessage = "Failure with secret token pin private key pfx data",
            },
            CancellationToken.None);

        string rawJson = await File.ReadAllTextAsync(service.StatusPath);

        rawJson.Should().Contain("agent-001");
        rawJson.Should().Contain("Mensagem sanitizada por conter dados sensiveis.");
        rawJson.Should().NotContain("secret");
        rawJson.Should().NotContain("token");
        rawJson.Should().NotContain("private key");
        rawJson.Should().NotContain("pfx");
    }

    [Fact]
    public async Task ReadStatusCombinesLocalConfigurationAndActivationFileWithoutExposingCredentials()
    {
        Directory.CreateDirectory(tempDirectory);
        const string localConfiguration = """
            {
              "AgentApi": {
                "BaseUrl": "https://api.mws.example"
              }
            }
            """;
        await File.WriteAllTextAsync(service.LocalConfigurationPath, localConfiguration);
        await File.WriteAllTextAsync(service.CredentialsPath, "encrypted-credential-bytes");
        await service.WriteStatusAsync(
            new AgentLocalStatusUpdate
            {
                AgentId = "agent-002",
                LastPollAt = DateTimeOffset.Parse("2026-05-14T11:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
            },
            CancellationToken.None);

        AgentLocalStatusSnapshot status = service.ReadStatus();
        string rawJson = await File.ReadAllTextAsync(service.StatusPath);

        status.Activated.Should().BeTrue();
        status.ApiBaseUrl.Should().Be(new Uri("https://api.mws.example/"));
        status.InstallationId.Should().Be("install-test");
        status.Version.Should().Be("9.9.9");
        rawJson.Should().NotContain("encrypted-credential-bytes");
    }

    [Fact]
    public void ServiceStateReturnsNotInstalledForMissingService()
    {
        service.ServiceState.Should().Be(AgentServiceState.NotInstalled);
    }

    private sealed class FakeAgentEnvironment : IAgentEnvironment
    {
        public string InstallationId => "install-test";

        public string MachineName => "machine-test";

        public string Version => "9.9.9";
    }
}
