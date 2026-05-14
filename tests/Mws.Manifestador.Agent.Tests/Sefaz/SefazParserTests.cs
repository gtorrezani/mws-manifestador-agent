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
