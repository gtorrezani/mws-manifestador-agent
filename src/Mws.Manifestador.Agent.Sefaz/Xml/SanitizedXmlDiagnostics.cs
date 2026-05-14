using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mws.Manifestador.Agent.Sefaz.Configuration;

namespace Mws.Manifestador.Agent.Sefaz.Xml;

public sealed class SanitizedXmlDiagnostics
{
    private static readonly Action<ILogger, string, string, string, string, Exception?> LogXmlDiagnostic =
        LoggerMessage.Define<string, string, string, string>(
            LogLevel.Debug,
            new EventId(3100, nameof(LogXmlDiagnostic)),
            "SEFAZ XML diagnostic {Direction} root={RootName} status={StatusCode} correlationId={CorrelationId}");

    private readonly ILogger<SanitizedXmlDiagnostics> logger;
    private readonly SefazOptions options;

    public SanitizedXmlDiagnostics(
        IOptions<SefazOptions> options,
        ILogger<SanitizedXmlDiagnostics> logger)
    {
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Log(string direction, string xml, string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(direction);
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        if (!options.DiagnosticXmlLogging)
        {
            return;
        }

        XDocument document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        string rootName = document.Root?.Name.LocalName ?? "unknown";
        string statusCode = document.Descendants().FirstOrDefault(static element => string.Equals(element.Name.LocalName, "cStat", StringComparison.Ordinal))?.Value ?? "none";
        LogXmlDiagnostic(logger, direction, rootName, statusCode, correlationId, null);
    }
}
