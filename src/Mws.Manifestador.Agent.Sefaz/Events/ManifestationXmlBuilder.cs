using System.Globalization;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using Mws.Manifestador.Agent.Sefaz.Configuration;
using Mws.Manifestador.Agent.Sefaz.Models;

namespace Mws.Manifestador.Agent.Sefaz.Events;

public sealed class ManifestationXmlBuilder
{
    private static readonly XNamespace Nfe = "http://www.portalfiscal.inf.br/nfe";
    private readonly SefazOptions options;

    public ManifestationXmlBuilder(IOptions<SefazOptions> options)
    {
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public string BuildBatch(IReadOnlyCollection<ManifestationEventRequest> events, string lotId)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentException.ThrowIfNullOrWhiteSpace(lotId);

        if (events.Count == 0 || events.Count > options.EventBatchLimit)
        {
            throw new InvalidOperationException($"Manifestation event batch must contain between 1 and {options.EventBatchLimit} events.");
        }

        XElement root = new(
            Nfe + "envEvento",
            new XAttribute("versao", "1.00"),
            new XElement(Nfe + "idLote", lotId));

        foreach (ManifestationEventRequest request in events)
        {
            root.Add(BuildEvent(request));
        }

        return root.ToString(SaveOptions.DisableFormatting);
    }

    public string BuildSingle(ManifestationEventRequest request, string lotId)
    {
        return BuildBatch([request], lotId);
    }

    private static XElement BuildEvent(ManifestationEventRequest request)
    {
        ValidateManifestation(request);

        string eventCode = ((int)request.EventCode).ToString(CultureInfo.InvariantCulture);
        string sequence = request.Sequence.ToString("00", CultureInfo.InvariantCulture);
        string eventId = $"ID{eventCode}{request.AccessKey.Value}{sequence}";

        XElement detEvento = new(
            Nfe + "detEvento",
            new XAttribute("versao", "1.00"),
            new XElement(Nfe + "descEvento", DescriptionFor(request.EventCode)));

        if (request.EventCode == ManifestationEventCode.OperationNotPerformed)
        {
            detEvento.Add(new XElement(Nfe + "xJust", request.Justification));
        }

        return new XElement(
            Nfe + "evento",
            new XAttribute("versao", "1.00"),
            new XElement(
                Nfe + "infEvento",
                new XAttribute("Id", eventId),
                new XElement(Nfe + "cOrgao", ((int)request.Uf).ToString(CultureInfo.InvariantCulture)),
                new XElement(Nfe + "tpAmb", request.Environment == Domain.Enums.SefazEnvironment.Production ? "1" : "2"),
                new XElement(Nfe + "CNPJ", request.Cnpj.Value),
                new XElement(Nfe + "chNFe", request.AccessKey.Value),
                new XElement(Nfe + "dhEvento", DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture)),
                new XElement(Nfe + "tpEvento", eventCode),
                new XElement(Nfe + "nSeqEvento", request.Sequence.ToString(CultureInfo.InvariantCulture)),
                new XElement(Nfe + "verEvento", "1.00"),
                detEvento));
    }

    private static void ValidateManifestation(ManifestationEventRequest request)
    {
        if (request.Sequence <= 0 || request.Sequence > 99)
        {
            throw new InvalidOperationException("Manifestation event sequence must be between 1 and 99.");
        }

        if (request.EventCode == ManifestationEventCode.OperationNotPerformed &&
            string.IsNullOrWhiteSpace(request.Justification))
        {
            throw new InvalidOperationException("Operation Not Performed manifestation requires justification.");
        }
    }

    private static string DescriptionFor(ManifestationEventCode code)
    {
        return code switch
        {
            ManifestationEventCode.OperationAcknowledgement => "Ciencia da Operacao",
            ManifestationEventCode.OperationConfirmation => "Confirmacao da Operacao",
            ManifestationEventCode.OperationUnknown => "Desconhecimento da Operacao",
            ManifestationEventCode.OperationNotPerformed => "Operacao nao Realizada",
            _ => throw new InvalidOperationException($"Unsupported manifestation event code '{code}'."),
        };
    }
}
