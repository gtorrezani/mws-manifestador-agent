using System.IO.Compression;
using System.Text;

namespace Mws.Manifestador.Agent.Sefaz.Parsing;

public sealed class NfeDocumentDecompressor
{
    public string DecompressDocZip(string base64GzipContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base64GzipContent);

        byte[] compressed = Convert.FromBase64String(base64GzipContent);
        using MemoryStream input = new(compressed);
        using GZipStream gzip = new(input, CompressionMode.Decompress);
        using MemoryStream output = new();
        gzip.CopyTo(output);
        return Encoding.UTF8.GetString(output.ToArray());
    }
}
