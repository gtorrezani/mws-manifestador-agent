using FluentAssertions;
using Microsoft.Extensions.Options;
using Mws.Manifestador.Agent.Sefaz.Configuration;
using Mws.Manifestador.Agent.Sefaz.Validation;

namespace Mws.Manifestador.Agent.Tests.Sefaz;

public sealed class XmlSchemaValidatorTests
{
    [Fact]
    public void ValidateAcceptsValidDistDfeIntWithOfficialSchema()
    {
        NfeXmlSchemaValidator validator = CreateValidator();

        XmlValidationResult result = validator.Validate("""
            <distDFeInt xmlns="http://www.portalfiscal.inf.br/nfe" versao="1.01">
              <tpAmb>2</tpAmb>
              <cUFAutor>35</cUFAutor>
              <CNPJ>12345678000195</CNPJ>
              <distNSU><ultNSU>000000000000000</ultNSU></distNSU>
            </distDFeInt>
            """);

        result.IsValid.Should().BeTrue();
        result.SchemaName.Should().Be("distDFeInt_v1.01.xsd");
    }

    [Fact]
    public void ValidateRejectsMalformedXml()
    {
        NfeXmlSchemaValidator validator = CreateValidator();

        XmlValidationResult result = validator.Validate("<distDFeInt>");

        result.IsValid.Should().BeFalse();
        result.Status.Should().Be(XmlValidationStatus.MalformedXml);
        validator.ShouldFail(result).Should().BeTrue();
    }

    [Fact]
    public void ValidateRejectsInvalidDistDfeInt()
    {
        NfeXmlSchemaValidator validator = CreateValidator();

        XmlValidationResult result = validator.Validate("""
            <distDFeInt xmlns="http://www.portalfiscal.inf.br/nfe" versao="1.01">
              <tpAmb>2</tpAmb>
              <cUFAutor>35</cUFAutor>
              <distNSU><ultNSU>000000000000000</ultNSU></distNSU>
            </distDFeInt>
            """);

        result.IsValid.Should().BeFalse();
        result.Status.Should().Be(XmlValidationStatus.InvalidXml);
        result.ValidationErrors.Should().NotBeEmpty();
    }

    [Fact]
    public void ValidateUnknownRootWarnsWhenStrictModeIsDisabled()
    {
        NfeXmlSchemaValidator validator = CreateValidator(strict: false, failOnUnknownSchema: false);

        XmlValidationResult result = validator.Validate("""
            <unknownNFe xmlns="http://www.portalfiscal.inf.br/nfe" versao="1.00" />
            """);

        result.Status.Should().Be(XmlValidationStatus.UnknownSchema);
        validator.ShouldFail(result).Should().BeFalse();
    }

    [Fact]
    public void ValidateUnknownRootFailsWhenStrictModeIsEnabled()
    {
        NfeXmlSchemaValidator validator = CreateValidator(strict: true, failOnUnknownSchema: true);

        XmlValidationResult result = validator.Validate("""
            <unknownNFe xmlns="http://www.portalfiscal.inf.br/nfe" versao="1.00" />
            """);

        result.Status.Should().Be(XmlValidationStatus.UnknownSchema);
        validator.ShouldFail(result).Should().BeTrue();
    }

    [Fact]
    public void ValidateAcceptsKnownDocZipSchemaWithOfficialSchema()
    {
        NfeXmlSchemaValidator validator = CreateValidator();

        XmlValidationResult result = validator.Validate(ValidSummaryXml(), "resNFe_v1.01.xsd");

        result.IsValid.Should().BeTrue();
        result.SchemaName.Should().Be("resNFe_v1.01.xsd");
    }

    [Fact]
    public void OfficialSchemasAreCopiedToBuildOutput()
    {
        string schemaPath = Path.Combine(AppContext.BaseDirectory, "Schemas", "NFe", "distDFeInt_v1.01.xsd");

        File.Exists(schemaPath).Should().BeTrue();
    }

    private static NfeXmlSchemaValidator CreateValidator(bool strict = false, bool failOnUnknownSchema = false)
    {
        return new NfeXmlSchemaValidator(Options.Create(new SefazOptions
        {
            SchemaValidation = new SchemaValidationOptions
            {
                Enabled = true,
                Strict = strict,
                SchemasPath = Path.Combine(AppContext.BaseDirectory, "Schemas", "NFe"),
                ValidateOutgoing = true,
                ValidateIncoming = true,
                FailOnUnknownSchema = failOnUnknownSchema,
            },
        }));
    }

    private static string ValidSummaryXml()
    {
        return """
            <resNFe xmlns="http://www.portalfiscal.inf.br/nfe" versao="1.01">
              <chNFe>35260512345678000195550010000000011000000010</chNFe>
              <CNPJ>12345678000195</CNPJ>
              <xNome>Emitente Homologacao</xNome>
              <IE>123456789012</IE>
              <dhEmi>2026-05-14T10:15:00-03:00</dhEmi>
              <tpNF>1</tpNF>
              <vNF>123.45</vNF>
              <dhRecbto>2026-05-14T10:20:00-03:00</dhRecbto>
              <nProt>135260000000001</nProt>
              <cSitNFe>1</cSitNFe>
            </resNFe>
            """;
    }
}
