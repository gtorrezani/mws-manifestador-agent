using System.Net.Mime;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Mws.Manifestador.Agent.Sefaz.Soap;

public sealed class SefazSoapTransport
{
    private readonly HttpClient httpClient;

    public SefazSoapTransport(HttpClient httpClient)
    {
        this.httpClient = httpClient;
    }

    public async Task<string> PostAsync(
        Uri endpoint,
        string soapAction,
        string envelopeXml,
        X509Certificate2? clientCertificate,
        CancellationToken cancellationToken)
    {
        if (clientCertificate is not null)
        {
            return await PostWithCertificateAsync(endpoint, soapAction, envelopeXml, clientCertificate, cancellationToken).ConfigureAwait(false);
        }

        return await PostWithClientAsync(httpClient, endpoint, soapAction, envelopeXml, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> PostWithCertificateAsync(
        Uri endpoint,
        string soapAction,
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

        return await PostWithClientAsync(certificateClient, endpoint, soapAction, envelopeXml, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> PostWithClientAsync(
        HttpClient client,
        Uri endpoint,
        string soapAction,
        string envelopeXml,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(envelopeXml, Encoding.UTF8, MediaTypeNames.Text.Xml),
        };
        request.Headers.Add("SOAPAction", soapAction);

        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        string responseXml = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        return responseXml;
    }
}
