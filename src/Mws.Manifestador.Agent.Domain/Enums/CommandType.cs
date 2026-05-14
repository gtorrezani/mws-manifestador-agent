namespace Mws.Manifestador.Agent.Domain.Enums;

public enum CommandType
{
    SyncFiscalDocuments,
    ManifestAcknowledgement,
    ManifestConfirmation,
    ManifestUnknown,
    ManifestNotPerformed,
    DownloadXmlByAccessKey,
    DownloadXmlByPeriod,
    ExportXmlZip,
    TestCertificate,
    ListCertificates,
    TestSefazConnectivity,
}
