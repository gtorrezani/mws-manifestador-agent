using System.Security.Cryptography.X509Certificates;
using Mws.Manifestador.Agent.Application.Certificates;

namespace Mws.Manifestador.Agent.Application.Interfaces;

public interface ICertificateProvider
{
    Task<IReadOnlyCollection<CertificateSummary>> ListAsync(CancellationToken cancellationToken);

    Task<X509Certificate2> GetCertificateAsync(CertificateReference reference, CancellationToken cancellationToken);
}
