using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Mws.Manifestador.Agent.Application.Configuration;
using Mws.Manifestador.Agent.Application.Interfaces;
using Mws.Manifestador.Agent.Infrastructure.Api;
using Mws.Manifestador.Agent.Infrastructure.LocalStatus;
using Mws.Manifestador.Agent.Infrastructure.Security;
using Mws.Manifestador.Agent.Infrastructure.Storage;
using Mws.Manifestador.Agent.Infrastructure.Windows;
using Polly;
using Polly.Extensions.Http;

namespace Mws.Manifestador.Agent.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<AgentApiOptions>(configuration.GetSection(AgentApiOptions.SectionName));
        services.Configure<AgentPollingOptions>(configuration.GetSection(AgentPollingOptions.SectionName));

        services.AddSingleton<IAgentCredentialStore, DpapiAgentCredentialStore>();
        services.AddSingleton<ProtectedPfxCertificateSecretStore>();
        services.AddSingleton<IAgentEnvironment, WindowsAgentEnvironment>();
        services.AddSingleton<AgentLocalStatusService>();
        services.AddSingleton<ITemporaryXmlStorage, LocalTemporaryXmlStorage>();

        services.AddHttpClient<IAgentApiClient, LaravelAgentApiClient>()
            .AddPolicyHandler(HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(3, static attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))));

        return services;
    }
}
