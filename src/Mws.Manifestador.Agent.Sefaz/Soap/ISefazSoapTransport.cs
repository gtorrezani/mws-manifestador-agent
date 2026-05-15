using System.Security.Cryptography.X509Certificates;
using Mws.Manifestador.Agent.Sefaz.Models;

namespace Mws.Manifestador.Agent.Sefaz.Soap;

public interface ISefazSoapTransport
{
    Task<string> PostAsync(
        SefazEndpoint endpoint,
        string envelopeXml,
        X509Certificate2? clientCertificate,
        CancellationToken cancellationToken);
}
