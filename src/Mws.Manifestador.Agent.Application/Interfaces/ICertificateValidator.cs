using Mws.Manifestador.Agent.Application.Certificates;

namespace Mws.Manifestador.Agent.Application.Interfaces;

public interface ICertificateValidator
{
    Task<CertificateValidationResult> ValidateAsync(CertificateReference reference, CancellationToken cancellationToken);
}
