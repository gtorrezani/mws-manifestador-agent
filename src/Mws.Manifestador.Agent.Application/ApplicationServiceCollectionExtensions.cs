using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Mws.Manifestador.Agent.Application.Certificates;
using Mws.Manifestador.Agent.Application.Commands;
using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Application.Interfaces;
using Mws.Manifestador.Agent.Application.Services;
using Mws.Manifestador.Agent.Application.Validation;
using Mws.Manifestador.Agent.Domain.Enums;

namespace Mws.Manifestador.Agent.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IValidator<ActivationRequest>, ActivationRequestValidator>();
        services.AddSingleton<AgentActivationService>();
        services.AddSingleton<HeartbeatService>();
        services.AddSingleton<CommandExecutor>();
        services.AddSingleton<PollingService>();
        services.AddSingleton<ICertificateSelector, InMemoryCompanyCertificateSelector>();
        services.AddSingleton<ICertificateValidator, CertificateValidator>();

        services.AddSingleton<ICommandHandler, ListCertificatesCommandHandler>();
        services.AddSingleton<ICommandHandler, TestCertificateCommandHandler>();
        AddSefazCommandHandler(services, CommandType.SyncFiscalDocuments);
        AddSefazCommandHandler(services, CommandType.ManifestAcknowledgement);
        AddSefazCommandHandler(services, CommandType.ManifestConfirmation);
        AddSefazCommandHandler(services, CommandType.ManifestUnknown);
        AddSefazCommandHandler(services, CommandType.ManifestNotPerformed);
        AddSefazCommandHandler(services, CommandType.DownloadXmlByAccessKey);
        AddSefazCommandHandler(services, CommandType.DownloadXmlByPeriod);
        AddSefazCommandHandler(services, CommandType.ExportXmlZip);
        AddSefazCommandHandler(services, CommandType.TestSefazConnectivity);

        return services;
    }

    private static void AddSefazCommandHandler(IServiceCollection services, CommandType commandType)
    {
        services.AddSingleton<ICommandHandler>(provider => new SefazCommandHandler(
            commandType,
            provider.GetRequiredService<ISefazClient>()));
    }
}
