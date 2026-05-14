using Mws.Manifestador.Agent.Domain.Entities;

namespace Mws.Manifestador.Agent.Application.Interfaces;

public interface ITemporaryXmlStorage
{
    Task<XmlArtifact> SaveAsync(string fileName, string xmlContent, CancellationToken cancellationToken);
}
