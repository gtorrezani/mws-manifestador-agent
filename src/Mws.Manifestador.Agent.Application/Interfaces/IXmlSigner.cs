using System.Security.Cryptography.X509Certificates;

namespace Mws.Manifestador.Agent.Application.Interfaces;

public interface IXmlSigner
{
    Task<string> SignAsync(string xmlContent, X509Certificate2 certificate, string referenceIdAttribute, CancellationToken cancellationToken);
}
