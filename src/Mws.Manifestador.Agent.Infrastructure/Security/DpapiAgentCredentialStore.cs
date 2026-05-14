using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Application.Interfaces;

namespace Mws.Manifestador.Agent.Infrastructure.Security;

[SupportedOSPlatform("windows")]
public sealed class DpapiAgentCredentialStore : IAgentCredentialStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string filePath;

    public DpapiAgentCredentialStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "MWS Manifestador Agent",
            "agent-credentials.dpapi"))
    {
    }

    public DpapiAgentCredentialStore(string filePath)
    {
        this.filePath = filePath;
    }

    public async Task<AgentCredentials?> GetAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        string protectedPayload = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        byte[] protectedBytes = Convert.FromBase64String(protectedPayload);
        byte[] bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.LocalMachine);
        StoredCredentials? stored = JsonSerializer.Deserialize<StoredCredentials>(bytes, JsonOptions);

        return stored is null ? null : new AgentCredentials(stored.AgentId, stored.Secret);
    }

    public async Task SaveAsync(AgentCredentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException("Credential path is invalid."));
        StoredCredentials stored = new(credentials.AgentId, credentials.Secret);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(stored, JsonOptions);
        byte[] protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.LocalMachine);
        await File.WriteAllTextAsync(filePath, Convert.ToBase64String(protectedBytes), cancellationToken).ConfigureAwait(false);
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    private sealed record StoredCredentials(Guid AgentId, string Secret);
}
