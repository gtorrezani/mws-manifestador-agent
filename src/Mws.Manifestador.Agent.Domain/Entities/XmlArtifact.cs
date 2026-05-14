namespace Mws.Manifestador.Agent.Domain.Entities;

public sealed record XmlArtifact(
    string StorageDisk,
    string StoragePath,
    string ContentHash);
