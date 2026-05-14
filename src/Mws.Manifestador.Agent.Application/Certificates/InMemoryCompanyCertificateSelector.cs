using Microsoft.Extensions.Configuration;
using Mws.Manifestador.Agent.Application.Interfaces;

namespace Mws.Manifestador.Agent.Application.Certificates;

public sealed class InMemoryCompanyCertificateSelector : ICertificateSelector
{
    private readonly IConfiguration configuration;

    public InMemoryCompanyCertificateSelector(IConfiguration configuration)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public Task<CertificateReference> SelectForCompanyAsync(string companyDocument, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(companyDocument);
        cancellationToken.ThrowIfCancellationRequested();

        string normalized = new(companyDocument.Where(char.IsDigit).ToArray());
        string? thumbprint = configuration[$"Certificates:Companies:{normalized}:Thumbprint"];
        if (string.IsNullOrWhiteSpace(thumbprint))
        {
            throw new InvalidOperationException($"No certificate thumbprint configured for company '{normalized}'.");
        }

        return Task.FromResult(CertificateReference.A3(thumbprint, companyDocument: normalized));
    }
}
