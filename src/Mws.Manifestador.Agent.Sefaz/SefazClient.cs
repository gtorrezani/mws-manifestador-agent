using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Mws.Manifestador.Agent.Application.Interfaces;
using Mws.Manifestador.Agent.Domain.Common;
using Mws.Manifestador.Agent.Domain.Entities;
using Mws.Manifestador.Agent.Domain.Enums;
using Mws.Manifestador.Agent.Sefaz.Distribution;
using Mws.Manifestador.Agent.Sefaz.Endpoints;
using Mws.Manifestador.Agent.Sefaz.Events;
using Mws.Manifestador.Agent.Sefaz.Models;
using Mws.Manifestador.Agent.Sefaz.Parsing;
using Mws.Manifestador.Agent.Sefaz.Soap;
using Mws.Manifestador.Agent.Sefaz.Validation;
using Mws.Manifestador.Agent.Sefaz.Xml;

namespace Mws.Manifestador.Agent.Sefaz;

public sealed class SefazClient : ISefazClient
{
    private static readonly Action<ILogger, string, string, Exception?> LogCallingSefaz =
        LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(3000, nameof(LogCallingSefaz)), "Calling SEFAZ service {Service} with correlationId {CorrelationId}");

    private readonly CommandPayloadReader payloadReader;
    private readonly DistributionXmlBuilder distributionXmlBuilder;
    private readonly ManifestationXmlBuilder manifestationXmlBuilder;
    private readonly IXmlSigner xmlSigner;
    private readonly NfeXmlSchemaValidator schemaValidator;
    private readonly SoapEnvelopeBuilder soapEnvelopeBuilder;
    private readonly SefazSoapTransport soapTransport;
    private readonly DistributionResponseParser distributionParser;
    private readonly EventResponseParser eventParser;
    private readonly ISefazEndpointResolver endpointResolver;
    private readonly ICertificateStore certificateStore;
    private readonly ITemporaryXmlStorage temporaryXmlStorage;
    private readonly SanitizedXmlDiagnostics xmlDiagnostics;
    private readonly ILogger<SefazClient> logger;

    public SefazClient(
        CommandPayloadReader payloadReader,
        DistributionXmlBuilder distributionXmlBuilder,
        ManifestationXmlBuilder manifestationXmlBuilder,
        IXmlSigner xmlSigner,
        NfeXmlSchemaValidator schemaValidator,
        SoapEnvelopeBuilder soapEnvelopeBuilder,
        SefazSoapTransport soapTransport,
        DistributionResponseParser distributionParser,
        EventResponseParser eventParser,
        ISefazEndpointResolver endpointResolver,
        ICertificateStore certificateStore,
        ITemporaryXmlStorage temporaryXmlStorage,
        SanitizedXmlDiagnostics xmlDiagnostics,
        ILogger<SefazClient> logger)
    {
        this.payloadReader = payloadReader ?? throw new ArgumentNullException(nameof(payloadReader));
        this.distributionXmlBuilder = distributionXmlBuilder ?? throw new ArgumentNullException(nameof(distributionXmlBuilder));
        this.manifestationXmlBuilder = manifestationXmlBuilder ?? throw new ArgumentNullException(nameof(manifestationXmlBuilder));
        this.xmlSigner = xmlSigner ?? throw new ArgumentNullException(nameof(xmlSigner));
        this.schemaValidator = schemaValidator ?? throw new ArgumentNullException(nameof(schemaValidator));
        this.soapEnvelopeBuilder = soapEnvelopeBuilder ?? throw new ArgumentNullException(nameof(soapEnvelopeBuilder));
        this.soapTransport = soapTransport ?? throw new ArgumentNullException(nameof(soapTransport));
        this.distributionParser = distributionParser ?? throw new ArgumentNullException(nameof(distributionParser));
        this.eventParser = eventParser ?? throw new ArgumentNullException(nameof(eventParser));
        this.endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
        this.certificateStore = certificateStore ?? throw new ArgumentNullException(nameof(certificateStore));
        this.temporaryXmlStorage = temporaryXmlStorage ?? throw new ArgumentNullException(nameof(temporaryXmlStorage));
        this.xmlDiagnostics = xmlDiagnostics ?? throw new ArgumentNullException(nameof(xmlDiagnostics));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<CommandExecutionResult>> SyncFiscalDocumentsAsync(AgentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            DistributionQuery query = payloadReader.ReadDistributionQuery(command);
            X509Certificate2 certificate = await certificateStore.FindByThumbprintAsync(query.CertificateThumbprint, cancellationToken).ConfigureAwait(false);
            string requestXml = distributionXmlBuilder.Build(query);
            Validate(requestXml);
            SefazEndpoint endpoint = endpointResolver.Resolve(SefazService.NFeDistribuicaoDFe, query.Environment, query.Uf);
            LogCallingSefaz(logger, endpoint.Service.ToString(), query.CorrelationId, null);
            Stopwatch stopwatch = Stopwatch.StartNew();
            string soapEnvelope = soapEnvelopeBuilder.Build(endpoint, requestXml);
            xmlDiagnostics.Log("request", requestXml, query.CorrelationId);
            string responseXml = await soapTransport.PostAsync(endpoint.Url, endpoint.SoapAction, soapEnvelope, certificate, cancellationToken).ConfigureAwait(false);
            xmlDiagnostics.Log("response", responseXml, query.CorrelationId);
            stopwatch.Stop();
            DistributionResponse response = distributionParser.Parse(responseXml);
            XmlArtifact requestArtifact = await temporaryXmlStorage.SaveAsync($"{query.CorrelationId}-dist-request.xml", requestXml, cancellationToken).ConfigureAwait(false);
            XmlArtifact responseArtifact = await temporaryXmlStorage.SaveAsync($"{query.CorrelationId}-dist-response.xml", responseXml, cancellationToken).ConfigureAwait(false);

            return Result.Success(new CommandExecutionResult(
                true,
                new
                {
                    response.LastNsu,
                    response.MaxNsu,
                    documents = response.Documents.Select(static document => new
                    {
                        document.Schema,
                        document.Nsu,
                        document.AccessKey,
                        document.Summary,
                        document.FullDocument,
                    }).ToArray(),
                },
                SefazStatusCode: response.Metadata.StatusCode,
                SefazMessage: response.Metadata.Reason,
                RequestXml: requestArtifact,
                ResponseXml: responseArtifact,
                DurationMs: (int)stopwatch.ElapsedMilliseconds));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Result.Failure<CommandExecutionResult>("SEFAZ_DISTRIBUTION_FAILED", exception.Message);
        }
    }

    public async Task<Result<CommandExecutionResult>> SendManifestationAsync(AgentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            ManifestationEventCode eventCode = EventCodeFromCommand(command.Type);
            ManifestationEventRequest request = payloadReader.ReadManifestationRequest(command, eventCode);
            X509Certificate2 certificate = await certificateStore.FindByThumbprintAsync(request.CertificateThumbprint, cancellationToken).ConfigureAwait(false);
            string requestXml = manifestationXmlBuilder.BuildSingle(request, LotIdFromCommand(command));
            string signedXml = await xmlSigner.SignAsync(requestXml, certificate, "Id", cancellationToken).ConfigureAwait(false);
            Validate(signedXml);
            SefazEndpoint endpoint = endpointResolver.Resolve(SefazService.NFeRecepcaoEvento, request.Environment, request.Uf);
            LogCallingSefaz(logger, endpoint.Service.ToString(), request.CorrelationId, null);
            Stopwatch stopwatch = Stopwatch.StartNew();
            string soapEnvelope = soapEnvelopeBuilder.Build(endpoint, signedXml);
            xmlDiagnostics.Log("request", signedXml, request.CorrelationId);
            string responseXml = await soapTransport.PostAsync(endpoint.Url, endpoint.SoapAction, soapEnvelope, certificate, cancellationToken).ConfigureAwait(false);
            xmlDiagnostics.Log("response", responseXml, request.CorrelationId);
            stopwatch.Stop();
            EventReceptionResponse response = eventParser.Parse(responseXml);
            XmlArtifact requestArtifact = await temporaryXmlStorage.SaveAsync($"{request.CorrelationId}-event-request.xml", signedXml, cancellationToken).ConfigureAwait(false);
            XmlArtifact responseArtifact = await temporaryXmlStorage.SaveAsync($"{request.CorrelationId}-event-response.xml", responseXml, cancellationToken).ConfigureAwait(false);

            return Result.Success(new CommandExecutionResult(
                true,
                new
                {
                    event_code = (int)eventCode,
                    response.EventStatusCode,
                    response.EventReason,
                    response.EventProtocolNumber,
                },
                response.EventProtocolNumber ?? response.Metadata.ProtocolNumber,
                response.EventStatusCode ?? response.Metadata.StatusCode,
                response.EventReason ?? response.Metadata.Reason,
                requestArtifact,
                responseArtifact,
                (int)stopwatch.ElapsedMilliseconds));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Result.Failure<CommandExecutionResult>("SEFAZ_EVENT_FAILED", exception.Message);
        }
    }

    public Task<Result<CommandExecutionResult>> DownloadXmlAsync(AgentCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SyncFiscalDocumentsAsync(command, cancellationToken);
    }

    public Task<Result<CommandExecutionResult>> TestConnectivityAsync(AgentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            DistributionQuery query = payloadReader.ReadDistributionQuery(command);
            SefazEndpoint endpoint = endpointResolver.Resolve(SefazService.NFeDistribuicaoDFe, query.Environment, query.Uf);

            return Task.FromResult(Result.Success(new CommandExecutionResult(
                true,
                new
                {
                    endpoint = endpoint.Url.ToString(),
                    endpoint.Service,
                    endpoint.Environment,
                })));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Task.FromResult(Result.Failure<CommandExecutionResult>("SEFAZ_CONNECTIVITY_CONFIGURATION_FAILED", exception.Message));
        }
    }

    private void Validate(string xml)
    {
        XmlValidationResult result = schemaValidator.Validate(xml);
        if (!result.IsValid)
        {
            throw new InvalidOperationException("NF-e XML schema validation failed: " + string.Join("; ", result.Errors));
        }
    }

    private static ManifestationEventCode EventCodeFromCommand(CommandType type)
    {
        return type switch
        {
            CommandType.ManifestAcknowledgement => ManifestationEventCode.OperationAcknowledgement,
            CommandType.ManifestConfirmation => ManifestationEventCode.OperationConfirmation,
            CommandType.ManifestUnknown => ManifestationEventCode.OperationUnknown,
            CommandType.ManifestNotPerformed => ManifestationEventCode.OperationNotPerformed,
            _ => throw new InvalidOperationException($"Command type '{type}' is not a manifestation event."),
        };
    }

    private static string LotIdFromCommand(AgentCommand command)
    {
        if (command.Payload.TryGetProperty("lot_id", out System.Text.Json.JsonElement element) &&
            element.ValueKind == System.Text.Json.JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(element.GetString()))
        {
            return element.GetString()!;
        }

        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
