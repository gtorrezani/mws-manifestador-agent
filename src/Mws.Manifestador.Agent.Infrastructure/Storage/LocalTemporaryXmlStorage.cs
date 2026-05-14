using System.Security.Cryptography;
using System.Text;
using Mws.Manifestador.Agent.Application.Interfaces;
using Mws.Manifestador.Agent.Domain.Entities;

namespace Mws.Manifestador.Agent.Infrastructure.Storage;

public sealed class LocalTemporaryXmlStorage : ITemporaryXmlStorage
{
    private readonly string rootPath;

    public LocalTemporaryXmlStorage()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "MWS Manifestador Agent",
            "xml-temp"))
    {
    }

    public LocalTemporaryXmlStorage(string rootPath)
    {
        this.rootPath = rootPath;
    }

    public async Task<XmlArtifact> SaveAsync(string fileName, string xmlContent, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(rootPath);
        string safeFileName = Path.GetFileName(fileName);
        string path = Path.Combine(rootPath, safeFileName);
        await File.WriteAllTextAsync(path, xmlContent, Encoding.UTF8, cancellationToken).ConfigureAwait(false);

        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(xmlContent))).ToUpperInvariant();
        return new XmlArtifact("local", path, hash);
    }
}
