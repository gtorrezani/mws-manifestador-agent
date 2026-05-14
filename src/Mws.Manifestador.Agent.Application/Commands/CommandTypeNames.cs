using Mws.Manifestador.Agent.Domain.Enums;

namespace Mws.Manifestador.Agent.Application.Commands;

public static class CommandTypeNames
{
    private static readonly Dictionary<CommandType, string> Names = new()
    {
        [CommandType.SyncFiscalDocuments] = "sync_fiscal_documents",
        [CommandType.ManifestAcknowledgement] = "manifest_acknowledgement",
        [CommandType.ManifestConfirmation] = "manifest_confirmation",
        [CommandType.ManifestUnknown] = "manifest_unknown",
        [CommandType.ManifestNotPerformed] = "manifest_not_performed",
        [CommandType.DownloadXmlByAccessKey] = "download_xml_by_access_key",
        [CommandType.DownloadXmlByPeriod] = "download_xml_by_period",
        [CommandType.ExportXmlZip] = "export_xml_zip",
        [CommandType.TestCertificate] = "test_certificate",
        [CommandType.ListCertificates] = "list_certificates",
        [CommandType.TestSefazConnectivity] = "test_sefaz_connectivity",
    };

    public static string ToWireName(CommandType type) => Names[type];

    public static CommandType FromWireName(string value)
    {
        CommandType? type = Names
            .Where(pair => string.Equals(pair.Value, value, StringComparison.Ordinal))
            .Select(static pair => (CommandType?)pair.Key)
            .FirstOrDefault();

        if (type is { } supportedType)
        {
            return supportedType;
        }

        throw new NotSupportedException($"Unsupported command type '{value}'.");
    }
}
