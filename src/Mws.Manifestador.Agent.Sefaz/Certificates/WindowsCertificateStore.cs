using System.Security.Cryptography.X509Certificates;
using Mws.Manifestador.Agent.Application.Certificates;
using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Application.Interfaces;

namespace Mws.Manifestador.Agent.Sefaz.Certificates;

public sealed class WindowsCertificateStore : ICertificateStore
{
    private readonly ICertificateProvider provider;

    public WindowsCertificateStore(ICertificateProvider provider)
    {
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
    }

    public async Task<IReadOnlyCollection<CertificateInfo>> ListAsync(CancellationToken cancellationToken)
    {
        IReadOnlyCollection<CertificateSummary> certificates = await provider.ListAsync(cancellationToken).ConfigureAwait(false);
        return certificates.Select(CertificateInfoMapper.ToCertificateInfo).ToArray();
    }

    public Task<X509Certificate2> FindByThumbprintAsync(string thumbprint, CancellationToken cancellationToken)
    {
        return provider.GetCertificateAsync(CertificateReference.A3(thumbprint), cancellationToken);
    }
}
