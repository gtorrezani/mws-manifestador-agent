using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Options;
using Mws.Manifestador.Agent.Application.Certificates;
using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Application.Interfaces;
using Mws.Manifestador.Agent.Domain.Entities;
using Mws.Manifestador.Agent.Domain.Enums;
using Mws.Manifestador.Agent.Sefaz.Configuration;
using Mws.Manifestador.Agent.Sefaz.Endpoints;
using Mws.Manifestador.Agent.Sefaz.Models;
using Mws.Manifestador.Agent.Sefaz.Parsing;
using Mws.Manifestador.Agent.Sefaz.Soap;
using Mws.Manifestador.Agent.Sefaz.Validation;
using Mws.Manifestador.Agent.Sefaz.Xml;

namespace Mws.Manifestador.Agent.Sefaz.Distribution;

public sealed class SyncFiscalDocumentsCommandHandler : ICommandHandler
{
    private static readonly string[] TrustedStatusCodes = ["137", "138"];

    private readonly CommandPayloadReader payloadReader;
    private readonly ICertificateProvider certificateProvider;
    private readonly DistributionXmlBuilder xmlBuilder;
    private readonly SoapEnvelopeBuilder soapEnvelopeBuilder;
    private readonly ISefazSoapTransport soapTransport;
    private readonly DistributionResponseParser responseParser;
    private readonly NfeXmlSchemaValidator schemaValidator;
    private readonly ISefazEndpointResolver endpointResolver;
    private readonly ITemporaryXmlStorage temporaryXmlStorage;
    private readonly SanitizedXmlDiagnostics xmlDiagnostics;
    private readonly SefazOptions options;

    public SyncFiscalDocumentsCommandHandler(
        CommandPayloadReader payloadReader,
        ICertificateProvider certificateProvider,
        DistributionXmlBuilder xmlBuilder,
        SoapEnvelopeBuilder soapEnvelopeBuilder,
        ISefazSoapTransport soapTransport,
        DistributionResponseParser responseParser,
        NfeXmlSchemaValidator schemaValidator,
        ISefazEndpointResolver endpointResolver,
        ITemporaryXmlStorage temporaryXmlStorage,
        SanitizedXmlDiagnostics xmlDiagnostics,
        IOptions<SefazOptions> options)
    {
        this.payloadReader = payloadReader ?? throw new ArgumentNullException(nameof(payloadReader));
        this.certificateProvider = certificateProvider ?? throw new ArgumentNullException(nameof(certificateProvider));
        this.xmlBuilder = xmlBuilder ?? throw new ArgumentNullException(nameof(xmlBuilder));
        this.soapEnvelopeBuilder = soapEnvelopeBuilder ?? throw new ArgumentNullException(nameof(soapEnvelopeBuilder));
        this.soapTransport = soapTransport ?? throw new ArgumentNullException(nameof(soapTransport));
        this.responseParser = responseParser ?? throw new ArgumentNullException(nameof(responseParser));
        this.schemaValidator = schemaValidator ?? throw new ArgumentNullException(nameof(schemaValidator));
        this.endpointResolver = endpointResolver ?? throw new ArgumentNullException(nameof(endpointResolver));
        this.temporaryXmlStorage = temporaryXmlStorage ?? throw new ArgumentNullException(nameof(temporaryXmlStorage));
        this.xmlDiagnostics = xmlDiagnostics ?? throw new ArgumentNullException(nameof(xmlDiagnostics));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public CommandType Type => CommandType.SyncFiscalDocuments;

#pragma warning disable MA0051
    public async Task<CommandExecutionOutcome> ExecuteAsync(AgentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        Stopwatch stopwatch = Stopwatch.StartNew();

        DistributionQuery query;
        try
        {
            query = payloadReader.ReadDistributionQuery(command);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            return Failure("SEFAZ_DISTRIBUTION_INVALID_PAYLOAD", exception.Message, null, null, stopwatch);
        }

        if (query.Environment == SefazEnvironment.Production && !options.AllowProductionDistribution)
        {
            return Failure(
                "SEFAZ_DISTRIBUTION_PRODUCTION_BLOCKED",
                "Production NFeDistribuicaoDFe is disabled for this agent. Use homologation or enable Sefaz:AllowProductionDistribution explicitly.",
                null,
                null,
                stopwatch);
        }

        try
        {
            using X509Certificate2 certificate = await certificateProvider.GetCertificateAsync(
                CertificateReference.A3(query.CertificateThumbprint, query.CertificateStoreScope),
                cancellationToken).ConfigureAwait(false);

            string requestXml = xmlBuilder.Build(query);
            XmlValidationResult? outgoingValidation = ValidateOutgoing(requestXml);
            if (outgoingValidation is not null && schemaValidator.ShouldFail(outgoingValidation))
            {
                return SchemaValidationFailure(outgoingValidation, query.CorrelationId, stopwatch);
            }

            SefazEndpoint endpoint = endpointResolver.Resolve(SefazService.NFeDistribuicaoDFe, query.Environment, query.Uf);
            string soapEnvelope = soapEnvelopeBuilder.Build(endpoint, requestXml);
            xmlDiagnostics.Log("request", requestXml, query.CorrelationId);

            string responseXml = await soapTransport.PostAsync(endpoint, soapEnvelope, certificate, cancellationToken).ConfigureAwait(false);
            xmlDiagnostics.Log("response", responseXml, query.CorrelationId);
            XmlArtifact requestArtifact = await temporaryXmlStorage.SaveAsync($"{query.CorrelationId}-dist-request.xml", requestXml, cancellationToken).ConfigureAwait(false);
            XmlArtifact responseArtifact = await temporaryXmlStorage.SaveAsync($"{query.CorrelationId}-dist-response.xml", responseXml, cancellationToken).ConfigureAwait(false);
            XmlValidationResult? incomingValidation = ValidateIncoming(responseXml);
            if (incomingValidation is not null && schemaValidator.ShouldFail(incomingValidation))
            {
                return SchemaValidationFailure(incomingValidation, query.CorrelationId, stopwatch, endpoint, requestArtifact, responseArtifact);
            }

            DistributionResponse response = responseParser.Parse(responseXml);
            XmlValidationResult? documentValidation = ValidateDocuments(response);
            if (documentValidation is not null && schemaValidator.ShouldFail(documentValidation))
            {
                return SchemaValidationFailure(documentValidation, query.CorrelationId, stopwatch, endpoint, requestArtifact, responseArtifact);
            }

            if (string.Equals(response.Metadata.StatusCode, "656", StringComparison.Ordinal))
            {
                return Failure(
                    "SEFAZ_DISTRIBUTION_CONSUMPTION_DENIED",
                    response.Metadata.Reason ?? "SEFAZ rejected distribution query as improper consumption.",
                    response.Metadata.StatusCode,
                    response.Metadata.Reason,
                    stopwatch,
                    endpoint,
                    requestArtifact,
                    responseArtifact,
                    distributionResult: "consumption_denied",
                    retryAfterHintMinutes: options.Distribution.ConsumptionDeniedRetryAfterMinutes);
            }

            if (!TrustedStatusCodes.Contains(response.Metadata.StatusCode, StringComparer.Ordinal))
            {
                return Failure(
                    "SEFAZ_DISTRIBUTION_REJECTED",
                    response.Metadata.Reason ?? "SEFAZ rejected distribution query.",
                    response.Metadata.StatusCode,
                    response.Metadata.Reason,
                    stopwatch,
                    endpoint,
                    requestArtifact,
                    responseArtifact);
            }

            return CommandExecutionOutcome.FromResult(new CommandExecutionResult(
                true,
                ToResultPayload(query, endpoint, response, stopwatch),
                SefazStatusCode: response.Metadata.StatusCode,
                SefazMessage: response.Metadata.Reason,
                RequestXml: requestArtifact,
                ResponseXml: responseArtifact,
                DurationMs: ElapsedMilliseconds(stopwatch)));
        }
        catch (CertificateProviderException exception)
        {
            return Failure(CertificateErrorCodeToWire(exception.ErrorCode), exception.Message, null, null, stopwatch);
        }
        catch (InvalidOperationException exception)
        {
            return Failure("SEFAZ_DISTRIBUTION_ENDPOINT_NOT_CONFIGURED", exception.Message, null, null, stopwatch);
        }
        catch (SefazSoapException exception)
        {
            return Failure("SEFAZ_DISTRIBUTION_SOAP_FAILED", exception.Message, null, null, stopwatch, httpStatusCode: exception.StatusCodeNumber);
        }
        catch (Exception exception) when (exception is FormatException or InvalidDataException or System.Xml.XmlException)
        {
            return Failure("SEFAZ_DISTRIBUTION_PARSE_FAILED", exception.Message, null, null, stopwatch);
        }
    }
#pragma warning restore MA0051

    private static object ToResultPayload(
        DistributionQuery query,
        SefazEndpoint endpoint,
        DistributionResponse response,
        Stopwatch stopwatch)
    {
        return new
        {
            service = "nfe_distribution",
            environment = query.Environment == SefazEnvironment.Homologation ? "homologation" : "production",
            uf = query.Uf.ToString(),
            endpoint = endpoint.Url.ToString(),
            soap_action = endpoint.SoapAction,
            correlation_id = query.CorrelationId,
            last_nsu = response.LastNsu,
            max_nsu = response.MaxNsu,
            sefaz_status_code = response.Metadata.StatusCode,
            sefaz_message = response.Metadata.Reason,
            distribution_result = DistributionResult(response.Metadata.StatusCode),
            retry_after_hint_minutes = RetryAfterHintMinutes(response.Metadata.StatusCode),
            duration_ms = ElapsedMilliseconds(stopwatch),
            documents = response.Documents.Select(document => ToDocumentPayload(document, query)).ToArray(),
        };
    }

    private static object ToDocumentPayload(DistributedDocument document, DistributionQuery query)
    {
        FiscalDocumentSummary? summary = document.Summary;
        FiscalDocumentFull? full = document.FullDocument;

        return new
        {
            nsu = document.Nsu,
            schema = document.Schema,
            access_key = document.AccessKey,
            issuer_cnpj = summary?.IssuerCnpj ?? full?.IssuerCnpj,
            issuer_name = summary?.IssuerName ?? full?.IssuerName,
            recipient_cnpj = full?.RecipientCnpj ?? query.Cnpj.Value,
            number = full?.Number,
            series = full?.Series,
            issued_at = FormatDate(summary?.IssuedAt ?? full?.IssuedAt),
            total_amount = FormatDecimal(summary?.TotalAmount ?? full?.TotalAmount),
            summary_xml = summary is null ? null : document.Xml,
            full_xml = full is null ? null : document.Xml,
            raw_xml = summary is null && full is null ? document.Xml : null,
            content_hash = Sha256(document.Xml),
        };
    }

    private XmlValidationResult? ValidateOutgoing(string requestXml)
    {
        return options.SchemaValidation.ValidateOutgoing
            ? schemaValidator.Validate(requestXml)
            : null;
    }

    private XmlValidationResult? ValidateIncoming(string responseXml)
    {
        return options.SchemaValidation.ValidateIncoming
            ? schemaValidator.Validate(responseXml)
            : null;
    }

    private XmlValidationResult? ValidateDocuments(DistributionResponse response)
    {
        if (!options.SchemaValidation.ValidateIncoming)
        {
            return null;
        }

        foreach (DistributedDocument document in response.Documents)
        {
            XmlValidationResult result = schemaValidator.Validate(document.Xml, document.Schema);
            if (schemaValidator.ShouldFail(result))
            {
                return result;
            }
        }

        return null;
    }

    private static CommandExecutionOutcome SchemaValidationFailure(
        XmlValidationResult validationResult,
        string correlationId,
        Stopwatch stopwatch,
        SefazEndpoint? endpoint = null,
        XmlArtifact? requestArtifact = null,
        XmlArtifact? responseArtifact = null)
    {
        return CommandExecutionOutcome.FromFailure(new CommandExecutionFailure(
            "SEFAZ_XML_SCHEMA_INVALID",
            "XML rejected by technical schema validation.",
            new
            {
                correlation_id = correlationId,
                schema_name = validationResult.SchemaName,
                root_element = validationResult.RootElement,
                validation_status = validationResult.Status.ToString(),
                validation_errors = validationResult.ValidationErrors.Select(static error => new
                {
                    message = error.Message,
                    line_number = error.LineNumber,
                    line_position = error.LinePosition,
                }).ToArray(),
                endpoint = endpoint?.Url.ToString(),
                soap_action = endpoint?.SoapAction,
                request_xml = requestArtifact,
                response_xml = responseArtifact,
            },
            DurationMs: ElapsedMilliseconds(stopwatch)));
    }

    private static CommandExecutionOutcome Failure(
        string errorCode,
        string errorMessage,
        string? sefazStatusCode,
        string? sefazMessage,
        Stopwatch stopwatch,
        SefazEndpoint? endpoint = null,
        XmlArtifact? requestArtifact = null,
        XmlArtifact? responseArtifact = null,
        int? httpStatusCode = null,
        string? distributionResult = null,
        int? retryAfterHintMinutes = null)
    {
        return CommandExecutionOutcome.FromFailure(new CommandExecutionFailure(
            errorCode,
            errorMessage,
            new
            {
                endpoint = endpoint?.Url.ToString(),
                soap_action = endpoint?.SoapAction,
                http_status_code = httpStatusCode,
                request_xml = requestArtifact,
                response_xml = responseArtifact,
                distribution_result = distributionResult,
                retry_after_hint_minutes = retryAfterHintMinutes,
            },
            sefazStatusCode,
            sefazMessage,
            ElapsedMilliseconds(stopwatch)));
    }

    private static string CertificateErrorCodeToWire(CertificateErrorCode errorCode)
    {
        return errorCode switch
        {
            CertificateErrorCode.CertificateNotFound => "SEFAZ_DISTRIBUTION_CERTIFICATE_NOT_FOUND",
            CertificateErrorCode.CertificateExpired => "SEFAZ_DISTRIBUTION_CERTIFICATE_INVALID",
            CertificateErrorCode.CertificateWithoutPrivateKey => "SEFAZ_DISTRIBUTION_CERTIFICATE_INVALID",
            CertificateErrorCode.CertificateInvalid => "SEFAZ_DISTRIBUTION_CERTIFICATE_INVALID",
            _ => "SEFAZ_DISTRIBUTION_CERTIFICATE_INVALID",
        };
    }

    private static string? DistributionResult(string? statusCode)
    {
        return statusCode switch
        {
            "137" => "no_documents",
            "138" => "documents_found",
            "656" => "consumption_denied",
            _ => "sefaz_rejection",
        };
    }

    private static int? RetryAfterHintMinutes(string? statusCode)
    {
        return string.Equals(statusCode, "656", StringComparison.Ordinal) ? 60 : null;
    }

    private static string? FormatDate(DateTimeOffset? value)
    {
        return value?.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }

    private static string? FormatDecimal(decimal? value)
    {
        return value?.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string Sha256(string value)
    {
#pragma warning disable CA1308
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLower(CultureInfo.InvariantCulture);
#pragma warning restore CA1308
    }

    private static int ElapsedMilliseconds(Stopwatch stopwatch)
    {
        return checked((int)Math.Min(int.MaxValue, stopwatch.ElapsedMilliseconds));
    }
}
