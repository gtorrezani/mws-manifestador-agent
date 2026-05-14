using FluentAssertions;
using Microsoft.Extensions.Options;
using Mws.Manifestador.Agent.Sefaz.Configuration;
using Mws.Manifestador.Agent.Sefaz.Validation;

namespace Mws.Manifestador.Agent.Tests.Sefaz;

public sealed class XmlSchemaValidatorTests
{
    [Fact]
    public void ValidateUsesConfiguredSchemaDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), "mws-xsd-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string schemaPath = Path.Combine(directory, "distDFeInt_v1.01.xsd");
        string schemaContent = """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
                       targetNamespace="http://www.portalfiscal.inf.br/nfe"
                       xmlns="http://www.portalfiscal.inf.br/nfe"
                       elementFormDefault="qualified">
              <xs:element name="distDFeInt">
                <xs:complexType>
                  <xs:sequence>
                    <xs:element name="tpAmb" type="xs:string"/>
                  </xs:sequence>
                  <xs:attribute name="versao" type="xs:string" use="required"/>
                </xs:complexType>
              </xs:element>
            </xs:schema>
            """;

        File.WriteAllText(schemaPath, schemaContent);
        NfeXmlSchemaValidator validator = new(Options.Create(new SefazOptions { SchemaDirectory = directory }));

        XmlValidationResult result = validator.Validate("""
            <distDFeInt xmlns="http://www.portalfiscal.inf.br/nfe" versao="1.01">
              <tpAmb>2</tpAmb>
            </distDFeInt>
            """);

        result.IsValid.Should().BeTrue();
    }
}
