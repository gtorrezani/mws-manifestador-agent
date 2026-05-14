namespace Mws.Manifestador.Agent.Application.Certificates;

public sealed record CertificateReference(
    CertificateKind Kind,
    string Thumbprint,
    CertificateStoreScope? StoreScope = null,
    string? CompanyDocument = null,
    string? FriendlyName = null)
{
    public static CertificateReference A3(string thumbprint, CertificateStoreScope? storeScope = null, string? companyDocument = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(thumbprint);
        return new CertificateReference(CertificateKind.A3, NormalizeThumbprint(thumbprint), storeScope, companyDocument);
    }

    public static CertificateReference A1(string thumbprint, string? companyDocument = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(thumbprint);
        return new CertificateReference(CertificateKind.A1, NormalizeThumbprint(thumbprint), null, companyDocument);
    }

    public static string NormalizeThumbprint(string thumbprint)
    {
        return new string(thumbprint.Where(static c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
    }
}
