using System.Net.Http.Headers;
using System.Net.Mime;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Mws.Manifestador.Agent.Sefaz.Models;

namespace Mws.Manifestador.Agent.Sefaz.Soap;

public sealed class SefazSoapTransport : ISefazSoapTransport
{
    private readonly HttpClient httpClient;

    public SefazSoapTransport(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<string> PostAsync(
        Uri endpoint,
        string soapAction,
        SoapVersion soapVersion,
        string envelopeXml,
        X509Certificate2? clientCertificate,
        CancellationToken cancellationToken)
    {
        if (clientCertificate is not null)
        {
            return await PostWithCertificateAsync(endpoint, soapAction, soapVersion, envelopeXml, clientCertificate, cancellationToken).ConfigureAwait(false);
        }

        return await PostWithClientAsync(httpClient, endpoint, soapAction, soapVersion, envelopeXml, cancellationToken).ConfigureAwait(false);
    }

    public Task<string> PostAsync(
        SefazEndpoint endpoint,
        string envelopeXml,
        X509Certificate2? clientCertificate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        return PostAsync(endpoint.Url, endpoint.SoapAction, endpoint.SoapVersion, envelopeXml, clientCertificate, cancellationToken);
    }

    private static async Task<string> PostWithCertificateAsync(
        Uri endpoint,
        string soapAction,
        SoapVersion soapVersion,
        string envelopeXml,
        X509Certificate2 clientCertificate,
        CancellationToken cancellationToken)
    {
        using HttpClientHandler handler = new()
        {
            CheckCertificateRevocationList = true,
        };
        handler.ClientCertificates.Add(clientCertificate);
        using HttpClient certificateClient = new(handler, disposeHandler: true);

        return await PostWithClientAsync(certificateClient, endpoint, soapAction, soapVersion, envelopeXml, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> PostWithClientAsync(
        HttpClient client,
        Uri endpoint,
        string soapAction,
        SoapVersion soapVersion,
        string envelopeXml,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(envelopeXml, Encoding.UTF8),
        };
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(ContentType(soapVersion, soapAction));
        if (soapVersion == SoapVersion.Soap11)
        {
            request.Headers.Add("SOAPAction", soapAction);
        }

        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        string responseXml = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new SefazSoapException((int)response.StatusCode, responseXml, $"SEFAZ SOAP request failed with HTTP status {(int)response.StatusCode}.");
        }

        return responseXml;
    }

    private static string ContentType(SoapVersion soapVersion, string soapAction)
    {
        return soapVersion == SoapVersion.Soap12
            ? $"application/soap+xml; charset=utf-8; action=\"{soapAction}\""
            : MediaTypeNames.Text.Xml;
    }
}
