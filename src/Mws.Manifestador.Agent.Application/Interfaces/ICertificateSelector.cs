using Mws.Manifestador.Agent.Application.Certificates;

namespace Mws.Manifestador.Agent.Application.Interfaces;

public interface ICertificateSelector
{
    Task<CertificateReference> SelectForCompanyAsync(string companyDocument, CancellationToken cancellationToken);
}
