using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Mws.Manifestador.Agent.Application.Certificates;
using Mws.Manifestador.Agent.Sefaz.Certificates;

namespace Mws.Manifestador.Agent.Tests.Sefaz;

public sealed class WindowsCertificateProviderClassificationTests
{
    [Fact]
    public void ToSummaryClassifiesFiscalCandidate()
    {
        using X509Certificate2 certificate = CreateCertificate(
            "CN=Empresa Teste:12.345.678/0001-95, O=ICP-Brasil",
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1));

        CertificateSummary summary = ToSummary(certificate);

        summary.IsFiscalCandidate.Should().BeTrue();
        summary.Classification.Should().Be("fiscal_candidate");
        summary.Document.Should().Be("12345678000195");
        summary.DocumentType.Should().Be("cnpj");
        summary.IsIcpBrasil.Should().BeTrue();
        summary.IsUsableForClientAuth.Should().BeTrue();
        summary.IsCertificateAuthority.Should().BeFalse();
        summary.RejectionReasons.Should().BeEmpty();
    }

    [Fact]
    public void ToSummaryClassifiesExpiredFiscalCertificate()
    {
        using X509Certificate2 certificate = CreateCertificate(
            "CN=Empresa Teste:12.345.678/0001-95, O=ICP-Brasil",
            DateTimeOffset.UtcNow.AddYears(-2),
            DateTimeOffset.UtcNow.AddDays(-1));

        CertificateSummary summary = ToSummary(certificate);

        summary.IsFiscalCandidate.Should().BeFalse();
        summary.Classification.Should().Be("expired_fiscal");
        summary.RejectionReasons.Should().Contain("Certificado vencido.");
    }

    [Fact]
    public void ToSummaryClassifiesCertificateWithoutPrivateKey()
    {
        using X509Certificate2 certificateWithKey = CreateCertificate(
            "CN=Empresa Teste:12.345.678/0001-95, O=ICP-Brasil",
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1));
        using X509Certificate2 certificate = new(certificateWithKey.Export(X509ContentType.Cert));

        CertificateSummary summary = ToSummary(certificate);

        summary.HasPrivateKey.Should().BeFalse();
        summary.IsFiscalCandidate.Should().BeFalse();
        summary.Classification.Should().Be("missing_private_key");
        summary.RejectionReasons.Should().Contain("Certificado sem chave privada.");
    }

    [Fact]
    public void ToSummaryClassifiesCertificateAuthority()
    {
        using X509Certificate2 certificate = CreateCertificate(
            "CN=AC Teste:12.345.678/0001-95, O=ICP-Brasil",
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1),
            isCertificateAuthority: true);

        CertificateSummary summary = ToSummary(certificate);

        summary.IsCertificateAuthority.Should().BeTrue();
        summary.IsFiscalCandidate.Should().BeFalse();
        summary.Classification.Should().Be("ca_certificate");
        summary.RejectionReasons.Should().Contain("Certificado de autoridade certificadora.");
    }

    [Fact]
    public void ToSummaryClassifiesSystemCertificate()
    {
        using X509Certificate2 certificate = CreateCertificate(
            "CN=Windows Admin Center",
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1));

        CertificateSummary summary = ToSummary(certificate);

        summary.IsFiscalCandidate.Should().BeFalse();
        summary.Classification.Should().Be("system_certificate");
        summary.RejectionReasons.Should().Contain("Emissor/cadeia nao indica ICP-Brasil.");
        summary.RejectionReasons.Should().Contain("CPF/CNPJ nao identificado no certificado.");
    }

    [Fact]
    public void ToSummaryClassifiesUnknownNonIcpBrasilCertificate()
    {
        using X509Certificate2 certificate = CreateCertificate(
            "CN=Empresa Sem Cadeia:12.345.678/0001-95",
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1));

        CertificateSummary summary = ToSummary(certificate);

        summary.IsFiscalCandidate.Should().BeFalse();
        summary.Classification.Should().Be("unknown");
        summary.RejectionReasons.Should().Contain("Emissor/cadeia nao indica ICP-Brasil.");
    }

    [Fact]
    public void ToSummaryExtractsCpfWhenPresent()
    {
        using X509Certificate2 certificate = CreateCertificate(
            "CN=Pessoa Teste:123.456.789-09, O=ICP-Brasil",
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1));

        CertificateSummary summary = ToSummary(certificate);

        summary.Document.Should().Be("12345678909");
        summary.DocumentType.Should().Be("cpf");
        summary.Cnpj.Should().BeNull();
    }

    private static X509Certificate2 CreateCertificate(
        string subject,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter,
        bool isCertificateAuthority = false)
    {
        using RSA rsa = RSA.Create(2048);
        CertificateRequest request = new(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(isCertificateAuthority, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection
            {
                new Oid("1.3.6.1.5.5.7.3.2"),
            },
            false));

        return request.CreateSelfSigned(notBefore, notAfter);
    }

    private static CertificateSummary ToSummary(X509Certificate2 certificate)
    {
        MethodInfo method = typeof(WindowsCertificateProvider).GetMethod("ToSummary", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("WindowsCertificateProvider.ToSummary was not found.");

        return (CertificateSummary)(method.Invoke(null, [certificate, CertificateStoreScope.CurrentUser])
            ?? throw new InvalidOperationException("WindowsCertificateProvider.ToSummary returned null."));
    }
}
