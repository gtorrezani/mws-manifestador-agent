using Mws.Manifestador.Agent.Domain.Enums;
using Mws.Manifestador.Agent.Sefaz.Models;

namespace Mws.Manifestador.Agent.Sefaz.Endpoints;

public sealed class SefazEndpointResolver : ISefazEndpointResolver
{
    private const string NfeNamespace = "http://www.portalfiscal.inf.br/nfe/wsdl";
    private static readonly Dictionary<(SefazService Service, SefazEnvironment Environment, SefazUf Uf), Uri> Endpoints = BuildEndpoints();

    public SefazEndpoint Resolve(SefazService service, SefazEnvironment environment, SefazUf uf)
    {
        SefazUf lookupUf = service == SefazService.NFeDistribuicaoDFe ? SefazUf.AN : uf;

        if (!Endpoints.TryGetValue((service, environment, lookupUf), out Uri? url) &&
            service == SefazService.NFeRecepcaoEvento)
        {
            lookupUf = SefazUf.AN;
            Endpoints.TryGetValue((service, environment, lookupUf), out url);
        }

        if (url is null)
        {
            throw new InvalidOperationException($"No SEFAZ endpoint configured for service '{service}', environment '{environment}' and UF '{uf}'.");
        }

        return service switch
        {
            SefazService.NFeDistribuicaoDFe => new SefazEndpoint(
                service,
                environment,
                lookupUf,
                url,
                $"{NfeNamespace}/NFeDistribuicaoDFe/nfeDistDFeInteresse",
                "nfeDistDFeInteresse",
                $"{NfeNamespace}/NFeDistribuicaoDFe"),
            SefazService.NFeRecepcaoEvento => new SefazEndpoint(
                service,
                environment,
                lookupUf,
                url,
                $"{NfeNamespace}/NFeRecepcaoEvento4/nfeRecepcaoEvento",
                "nfeRecepcaoEvento",
                $"{NfeNamespace}/NFeRecepcaoEvento4"),
            _ => throw new InvalidOperationException($"Unsupported SEFAZ service '{service}'."),
        };
    }

    private static Dictionary<(SefazService Service, SefazEnvironment Environment, SefazUf Uf), Uri> BuildEndpoints()
    {
        Dictionary<(SefazService, SefazEnvironment, SefazUf), Uri> endpoints = [];

        endpoints[(SefazService.NFeDistribuicaoDFe, SefazEnvironment.Production, SefazUf.AN)] =
            new Uri("https://www1.nfe.fazenda.gov.br/NFeDistribuicaoDFe/NFeDistribuicaoDFe.asmx");
        endpoints[(SefazService.NFeDistribuicaoDFe, SefazEnvironment.Homologation, SefazUf.AN)] =
            new Uri("https://hom1.nfe.fazenda.gov.br/NFeDistribuicaoDFe/NFeDistribuicaoDFe.asmx");

        AddEventEndpoints(endpoints, SefazEnvironment.Production, new Dictionary<SefazUf, string>
        {
            [SefazUf.AN] = "https://www.nfe.fazenda.gov.br/NFeRecepcaoEvento4/NFeRecepcaoEvento4.asmx",
            [SefazUf.SP] = "https://nfe.fazenda.sp.gov.br/ws/nferecepcaoevento4.asmx",
            [SefazUf.PR] = "https://nfe.sefa.pr.gov.br/nfe/NFeRecepcaoEvento4",
            [SefazUf.RS] = "https://nfe.sefazrs.rs.gov.br/ws/recepcaoevento/recepcaoevento4.asmx",
            [SefazUf.MG] = "https://nfe.fazenda.mg.gov.br/nfe2/services/NFeRecepcaoEvento4",
            [SefazUf.BA] = "https://nfe.sefaz.ba.gov.br/webservices/NFeRecepcaoEvento4/NFeRecepcaoEvento4.asmx",
        });

        AddEventEndpoints(endpoints, SefazEnvironment.Homologation, new Dictionary<SefazUf, string>
        {
            [SefazUf.AN] = "https://hom1.nfe.fazenda.gov.br/NFeRecepcaoEvento4/NFeRecepcaoEvento4.asmx",
            [SefazUf.SP] = "https://homologacao.nfe.fazenda.sp.gov.br/ws/nferecepcaoevento4.asmx",
            [SefazUf.PR] = "https://homologacao.nfe.sefa.pr.gov.br/nfe/NFeRecepcaoEvento4",
            [SefazUf.RS] = "https://nfe-homologacao.sefazrs.rs.gov.br/ws/recepcaoevento/recepcaoevento4.asmx",
            [SefazUf.MG] = "https://hnfe.fazenda.mg.gov.br/nfe2/services/NFeRecepcaoEvento4",
        });

        return endpoints;
    }

    private static void AddEventEndpoints(
        IDictionary<(SefazService Service, SefazEnvironment Environment, SefazUf Uf), Uri> endpoints,
        SefazEnvironment environment,
        IReadOnlyDictionary<SefazUf, string> urls)
    {
        foreach (KeyValuePair<SefazUf, string> item in urls)
        {
            endpoints[(SefazService.NFeRecepcaoEvento, environment, item.Key)] = new Uri(item.Value);
        }
    }
}
