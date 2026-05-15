using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Mws.Manifestador.Agent.Sefaz.Parsing;

namespace Mws.Manifestador.Agent.Tests.Sefaz;

public sealed class SefazParserTests
{
    [Fact]
    public void DecompressorInflatesDocZipContent()
    {
        string xml = "<resNFe xmlns=\"http://www.portalfiscal.inf.br/nfe\"><chNFe>1</chNFe></resNFe>";
        string compressed = Compress(xml);

        string result = new NfeDocumentDecompressor().DecompressDocZip(compressed);

        result.Should().Be(xml);
    }

    [Fact]
    public void FiscalDocumentParserParsesSummary()
    {
        string xml = """
            <resNFe xmlns="http://www.portalfiscal.inf.br/nfe">
              <chNFe>12345678901234567890123456789012345678901234</chNFe>
              <CNPJ>11111111000191</CNPJ>
              <xNome>Emitente Teste</xNome>
              <dhEmi>2026-05-14T10:15:00-03:00</dhEmi>
              <vNF>123.45</vNF>
              <cSitNFe>1</cSitNFe>
            </resNFe>
            """;

        Mws.Manifestador.Agent.Sefaz.Models.FiscalDocumentSummary? summary = new FiscalDocumentParser().TryParseSummary(xml);

        summary.Should().NotBeNull();
        summary?.AccessKey.Should().Be("12345678901234567890123456789012345678901234");
        summary?.TotalAmount.Should().Be(123.45m);
    }

    [Fact]
    public void DistributionResponseParserParsesNoDocumentsResponse()
    {
        string xml = """
            <soap12:Envelope xmlns:soap12="http://www.w3.org/2003/05/soap-envelope">
              <soap12:Body>
                <nfeDistDFeInteresseResponse xmlns="http://www.portalfiscal.inf.br/nfe/wsdl/NFeDistribuicaoDFe">
                  <nfeDistDFeInteresseResult>
                    <retDistDFeInt xmlns="http://www.portalfiscal.inf.br/nfe" versao="1.01">
                      <cStat>137</cStat>
                      <xMotivo>Nenhum documento localizado</xMotivo>
                      <ultNSU>000000000000010</ultNSU>
                      <maxNSU>000000000000010</maxNSU>
                    </retDistDFeInt>
                  </nfeDistDFeInteresseResult>
                </nfeDistDFeInteresseResponse>
              </soap12:Body>
            </soap12:Envelope>
            """;

        Mws.Manifestador.Agent.Sefaz.Models.DistributionResponse response = new DistributionResponseParser(new NfeDocumentDecompressor(), new FiscalDocumentParser()).Parse(xml);

        response.Metadata.StatusCode.Should().Be("137");
        response.Documents.Should().BeEmpty();
        response.LastNsu.Should().Be("000000000000010");
    }

    [Fact]
    public void DistributionResponseParserParsesDocZipSummary()
    {
        string summaryXml = """
            <resNFe xmlns="http://www.portalfiscal.inf.br/nfe">
              <chNFe>35260512345678000195550010000000011000000010</chNFe>
              <CNPJ>12345678000195</CNPJ>
              <xNome>Emitente Homologacao</xNome>
              <dhEmi>2026-05-14T10:15:00-03:00</dhEmi>
              <vNF>123.45</vNF>
              <cSitNFe>1</cSitNFe>
            </resNFe>
            """;
        string xml = $$"""
            <retDistDFeInt xmlns="http://www.portalfiscal.inf.br/nfe" versao="1.01">
              <cStat>138</cStat>
              <xMotivo>Documento localizado</xMotivo>
              <ultNSU>000000000000011</ultNSU>
              <maxNSU>000000000000011</maxNSU>
              <loteDistDFeInt>
                <docZip NSU="000000000000011" schema="resNFe_v1.01.xsd">{{Compress(summaryXml)}}</docZip>
              </loteDistDFeInt>
            </retDistDFeInt>
            """;

        Mws.Manifestador.Agent.Sefaz.Models.DistributionResponse response = new DistributionResponseParser(new NfeDocumentDecompressor(), new FiscalDocumentParser()).Parse(xml);

        response.Metadata.StatusCode.Should().Be("138");
        response.Documents.Should().ContainSingle();
        response.Documents.Single().Summary?.AccessKey.Should().Be("35260512345678000195550010000000011000000010");
    }

    [Fact]
    public void DistributionResponseParserPreservesUnknownSchemaXml()
    {
        string unknownXml = "<procEventoNFe xmlns=\"http://www.portalfiscal.inf.br/nfe\"><dummy /></procEventoNFe>";
        string xml = $$"""
            <retDistDFeInt xmlns="http://www.portalfiscal.inf.br/nfe" versao="1.01">
              <cStat>138</cStat>
              <xMotivo>Documento localizado</xMotivo>
              <ultNSU>000000000000012</ultNSU>
              <maxNSU>000000000000012</maxNSU>
              <loteDistDFeInt>
                <docZip NSU="000000000000012" schema="procEventoNFe_v1.00.xsd">{{Compress(unknownXml)}}</docZip>
              </loteDistDFeInt>
            </retDistDFeInt>
            """;

        Mws.Manifestador.Agent.Sefaz.Models.DistributionResponse response = new DistributionResponseParser(new NfeDocumentDecompressor(), new FiscalDocumentParser()).Parse(xml);

        response.Documents.Single().Xml.Should().Be(unknownXml);
        response.Documents.Single().Summary.Should().BeNull();
        response.Documents.Single().FullDocument.Should().BeNull();
    }

    [Fact]
    public void DistributionResponseParserFailsForInvalidDocZip()
    {
        string xml = """
            <retDistDFeInt xmlns="http://www.portalfiscal.inf.br/nfe" versao="1.01">
              <cStat>138</cStat>
              <xMotivo>Documento localizado</xMotivo>
              <ultNSU>000000000000012</ultNSU>
              <maxNSU>000000000000012</maxNSU>
              <loteDistDFeInt>
                <docZip NSU="000000000000012" schema="resNFe_v1.01.xsd">not-base64</docZip>
              </loteDistDFeInt>
            </retDistDFeInt>
            """;

        Action act = () => new DistributionResponseParser(new NfeDocumentDecompressor(), new FiscalDocumentParser()).Parse(xml);

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void EventResponseParserReadsProtocolAndStatus()
    {
        string xml = """
            <retEnvEvento xmlns="http://www.portalfiscal.inf.br/nfe" versao="1.00">
              <cStat>128</cStat><xMotivo>Lote de Evento Processado</xMotivo>
              <retEvento versao="1.00"><infEvento>
                <cStat>135</cStat><xMotivo>Evento registrado e vinculado a NF-e</xMotivo><nProt>135260000000001</nProt>
              </infEvento></retEvento>
            </retEnvEvento>
            """;

        Mws.Manifestador.Agent.Sefaz.Models.EventReceptionResponse response = new EventResponseParser().Parse(xml);

        response.EventStatusCode.Should().Be("135");
        response.EventProtocolNumber.Should().Be("135260000000001");
    }

    private static string Compress(string xml)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(xml);
        using MemoryStream output = new();
        using (GZipStream gzip = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(bytes);
        }

        return Convert.ToBase64String(output.ToArray());
    }
}
