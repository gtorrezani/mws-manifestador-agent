using FluentAssertions;
using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Infrastructure.Security;

namespace Mws.Manifestador.Agent.Tests.Infrastructure;

public sealed class DpapiAgentCredentialStoreTests
{
    [Fact]
    public async Task SaveAsyncAndGetAsyncRoundtripCredentials()
    {
        string filePath = Path.Combine(Path.GetTempPath(), "mws-agent-tests", Guid.NewGuid().ToString("N"), "credentials.dpapi");
        DpapiAgentCredentialStore store = new(filePath);
        AgentCredentials credentials = new(Guid.NewGuid(), "secret-value");

        await store.SaveAsync(credentials, CancellationToken.None);
        AgentCredentials? loaded = await store.GetAsync(CancellationToken.None);

        loaded.Should().Be(credentials);
        string raw = await File.ReadAllTextAsync(filePath, CancellationToken.None);
        raw.Should().NotContain("secret-value");
    }
}
