using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mws.Manifestador.Agent.Application.Interfaces;
using Mws.Manifestador.Agent.Sefaz.Certificates;
using Mws.Manifestador.Agent.Sefaz.Configuration;
using Mws.Manifestador.Agent.Sefaz.Distribution;
using Mws.Manifestador.Agent.Sefaz.Endpoints;
using Mws.Manifestador.Agent.Sefaz.Events;
using Mws.Manifestador.Agent.Sefaz.Parsing;
using Mws.Manifestador.Agent.Sefaz.Soap;
using Mws.Manifestador.Agent.Sefaz.Validation;
using Mws.Manifestador.Agent.Sefaz.Xml;

namespace Mws.Manifestador.Agent.Sefaz;

public static class SefazServiceCollectionExtensions
{
    public static IServiceCollection AddSefazServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<SefazOptions>(configuration.GetSection(SefazOptions.SectionName));
        services.AddSingleton<ICertificateProvider, WindowsCertificateProvider>();
        services.AddSingleton<ICertificateStore, WindowsCertificateStore>();
        services.AddSingleton<IXmlSigner, XmlSigner>();
        services.AddSingleton<SanitizedXmlDiagnostics>();
        services.AddSingleton<CommandPayloadReader>();
        services.AddSingleton<DistributionXmlBuilder>();
        services.AddSingleton<ManifestationXmlBuilder>();
        services.AddSingleton<NfeXmlSchemaValidator>();
        services.AddSingleton<SoapEnvelopeBuilder>();
        services.AddSingleton<NfeDocumentDecompressor>();
        services.AddSingleton<FiscalDocumentParser>();
        services.AddSingleton<DistributionResponseParser>();
        services.AddSingleton<EventResponseParser>();
        services.AddSingleton<ISefazEndpointResolver, SefazEndpointResolver>();
        services.AddSingleton<ISefazClient, SefazClient>();
        services.AddHttpClient<SefazSoapTransport>();

        return services;
    }
}
