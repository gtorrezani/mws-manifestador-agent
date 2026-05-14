using System.Security.Cryptography.X509Certificates;
using Mws.Manifestador.Agent.Application.DTOs;

namespace Mws.Manifestador.Agent.Application.Interfaces;

public interface ICertificateStore
{
    Task<IReadOnlyCollection<CertificateInfo>> ListAsync(CancellationToken cancellationToken);

    Task<X509Certificate2> FindByThumbprintAsync(string thumbprint, CancellationToken cancellationToken);
}
