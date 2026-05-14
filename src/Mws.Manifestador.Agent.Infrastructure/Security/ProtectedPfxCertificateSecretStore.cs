using System.Security.Cryptography;
using System.Text.Json;
using Mws.Manifestador.Agent.Application.Certificates;

namespace Mws.Manifestador.Agent.Infrastructure.Security;

public sealed class ProtectedPfxCertificateSecretStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string rootPath;

    public ProtectedPfxCertificateSecretStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "MWS Manifestador Agent",
            "certificates"))
    {
    }

    public ProtectedPfxCertificateSecretStore(string rootPath)
    {
        this.rootPath = rootPath;
    }

    public async Task<CertificateSecret> SaveAsync(
        CertificateReference reference,
        byte[] pfxBytes,
        string password,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(pfxBytes);
        ArgumentNullException.ThrowIfNull(password);

        if (reference.Kind != CertificateKind.A1)
        {
            throw new InvalidOperationException("Only A1 certificate secrets can be stored as protected PFX payloads.");
        }

        Directory.CreateDirectory(rootPath);
        StoredPfxPayload payload = new(
            Convert.ToBase64String(pfxBytes),
            password,
            DateTimeOffset.UtcNow);
        byte[] clearBytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        byte[] protectedBytes = ProtectedData.Protect(clearBytes, null, DataProtectionScope.LocalMachine);
        string filePath = Path.Combine(rootPath, reference.Thumbprint + ".pfx.dpapi");
        await File.WriteAllBytesAsync(filePath, protectedBytes, cancellationToken).ConfigureAwait(false);

        return new CertificateSecret(CertificateKind.A1, filePath, "windows-dpapi-local-machine", DateTimeOffset.UtcNow);
    }

    private sealed record StoredPfxPayload(string PfxBase64, string Password, DateTimeOffset CreatedAt);
}
