using FluentAssertions;
using Mws.Manifestador.Agent.Application.Certificates;
using Mws.Manifestador.Agent.Application.Interfaces;

namespace Mws.Manifestador.Agent.Tests.Certificates;

public sealed class CertificateValidatorTests
{
    [Fact]
    public async Task ValidateAsyncReturnsExpiredWhenCertificateIsExpired()
    {
        CertificateReference reference = CertificateReference.A3("ABC");
        CertificateSummary summary = CreateSummary(reference, DateTimeOffset.UtcNow.AddYears(-2), DateTimeOffset.UtcNow.AddDays(-1), true);
        CertificateValidator validator = new(new FakeCertificateProvider([summary]));

        CertificateValidationResult result = await validator.ValidateAsync(reference, CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(CertificateErrorCode.CertificateExpired);
    }

    [Fact]
    public async Task ValidateAsyncReturnsWithoutPrivateKeyWhenMissingPrivateKey()
    {
        CertificateReference reference = CertificateReference.A3("ABC");
        CertificateSummary summary = CreateSummary(reference, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1), false);
        CertificateValidator validator = new(new FakeCertificateProvider([summary]));

        CertificateValidationResult result = await validator.ValidateAsync(reference, CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(CertificateErrorCode.CertificateWithoutPrivateKey);
    }

    [Fact]
    public async Task ValidateAsyncReturnsValidForUsableCertificate()
    {
        CertificateReference reference = CertificateReference.A3("ABC");
        CertificateSummary summary = CreateSummary(reference, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1), true);
        CertificateValidator validator = new(new FakeCertificateProvider([summary]));

        CertificateValidationResult result = await validator.ValidateAsync(reference, CancellationToken.None);

        result.IsValid.Should().BeTrue();
        result.Certificate.Should().Be(summary);
    }

    [Fact]
    public async Task ValidateAsyncRejectsNonFiscalCandidateWithRejectionReason()
    {
        CertificateReference reference = CertificateReference.A3("ABC");
        CertificateSummary summary = CreateSummary(
            reference,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1),
            true,
            isFiscalCandidate: false,
            classification: "system_certificate",
            rejectionReasons: ["Emissor/cadeia nao indica ICP-Brasil."]);
        CertificateValidator validator = new(new FakeCertificateProvider([summary]));

        CertificateValidationResult result = await validator.ValidateAsync(reference, CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.ErrorCode.Should().Be(CertificateErrorCode.CertificateInvalid);
        result.Message.Should().Be("Emissor/cadeia nao indica ICP-Brasil.");
    }

    private static CertificateSummary CreateSummary(
        CertificateReference reference,
        DateTimeOffset notBefore,
        DateTimeOffset notAfter,
        bool hasPrivateKey,
        bool isFiscalCandidate = true,
        string classification = "fiscal_candidate",
        IReadOnlyCollection<string>? rejectionReasons = null)
    {
        return new CertificateSummary(
            reference,
            "CN=Test:12345678000195",
            "CN=Issuer",
            reference.Thumbprint,
            "123",
            notBefore,
            notAfter,
            hasPrivateKey,
            "12345678000195",
            CertificateStoreScope.CurrentUser,
            "Test",
            "12345678000195",
            "cnpj",
            false,
            true,
            true,
            isFiscalCandidate,
            classification,
            rejectionReasons ?? [],
            isFiscalCandidate ? ["Tipo A1/A3 nao confirmado automaticamente."] : []);
    }

    private sealed class FakeCertificateProvider : ICertificateProvider
    {
        private readonly IReadOnlyCollection<CertificateSummary> certificates;

        public FakeCertificateProvider(IReadOnlyCollection<CertificateSummary> certificates)
        {
            this.certificates = certificates;
        }

        public Task<IReadOnlyCollection<CertificateSummary>> ListAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(certificates);
        }

        public Task<System.Security.Cryptography.X509Certificates.X509Certificate2> GetCertificateAsync(
            CertificateReference reference,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Not needed for validator tests.");
        }
    }
}
