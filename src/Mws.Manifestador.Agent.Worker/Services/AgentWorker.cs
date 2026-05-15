using System.Text.Json;
using Microsoft.Extensions.Options;
using Mws.Manifestador.Agent.Application.Configuration;
using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Application.Interfaces;
using Mws.Manifestador.Agent.Application.Services;
using Mws.Manifestador.Agent.Infrastructure.LocalStatus;

namespace Mws.Manifestador.Agent.Worker.Services;

public sealed class AgentWorker : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogStarting =
        LoggerMessage.Define(LogLevel.Information, new EventId(1000, nameof(LogStarting)), "MWS Manifestador Agent starting");

    private static readonly Action<ILogger, Exception?> LogNotActivated =
        LoggerMessage.Define(LogLevel.Warning, new EventId(1001, nameof(LogNotActivated)), "Agent is not activated; configure AgentApi:ActivationCode to activate it");

    private static readonly Action<ILogger, int, Exception?> LogPollingCompleted =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(1002, nameof(LogPollingCompleted)), "Polling cycle completed with {ProcessedCommandCount} command(s)");

    private static readonly Action<ILogger, Exception?> LogUnableToWriteLocalStatus =
        LoggerMessage.Define(LogLevel.Warning, new EventId(1003, nameof(LogUnableToWriteLocalStatus)), "Unable to write local Agent status.");

    private readonly AgentActivationService activationService;
    private readonly AgentApiOptions apiOptions;
    private readonly IAgentEnvironment environment;
    private readonly HeartbeatService heartbeatService;
    private readonly AgentLocalStatusService localStatusService;
    private readonly ILogger<AgentWorker> logger;
    private readonly AgentPollingOptions options;
    private readonly PollingService pollingService;

    public AgentWorker(
        AgentActivationService activationService,
        HeartbeatService heartbeatService,
        PollingService pollingService,
        AgentLocalStatusService localStatusService,
        IAgentEnvironment environment,
        IOptions<AgentApiOptions> apiOptions,
        IOptions<AgentPollingOptions> options,
        ILogger<AgentWorker> logger)
    {
        this.activationService = activationService ?? throw new ArgumentNullException(nameof(activationService));
        this.heartbeatService = heartbeatService ?? throw new ArgumentNullException(nameof(heartbeatService));
        this.pollingService = pollingService ?? throw new ArgumentNullException(nameof(pollingService));
        this.localStatusService = localStatusService ?? throw new ArgumentNullException(nameof(localStatusService));
        this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
        this.apiOptions = apiOptions?.Value ?? throw new ArgumentNullException(nameof(apiOptions));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarting(logger, null);
        await WriteStartupStatusAsync(stoppingToken).ConfigureAwait(false);
        DateTimeOffset nextHeartbeatAt = DateTimeOffset.MinValue;

        using PeriodicTimer timer = new(TimeSpan.FromSeconds(options.IntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                nextHeartbeatAt = await ExecutePollingCycleAsync(nextHeartbeatAt, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                await WriteLocalStatusAsync(
                    new AgentLocalStatusUpdate
                    {
                        LastErrorMessage = exception.Message,
                    },
                    CancellationToken.None).ConfigureAwait(false);

                throw;
            }

            await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task<DateTimeOffset> ExecutePollingCycleAsync(DateTimeOffset nextHeartbeatAt, CancellationToken stoppingToken)
    {
        AgentCredentials? credentials = await activationService.EnsureActivatedAsync(stoppingToken).ConfigureAwait(false);
        if (credentials is null)
        {
            LogNotActivated(logger, null);
            await WriteLocalStatusAsync(
                new AgentLocalStatusUpdate
                {
                    ApiBaseUrl = apiOptions.BaseUrl,
                    Activated = false,
                },
                stoppingToken).ConfigureAwait(false);

            return nextHeartbeatAt;
        }

        await WriteActivatedStatusAsync(credentials, stoppingToken).ConfigureAwait(false);
        DateTimeOffset updatedNextHeartbeatAt = await SendHeartbeatIfNeededAsync(credentials, nextHeartbeatAt, stoppingToken).ConfigureAwait(false);
        await PollCommandsAsync(credentials, stoppingToken).ConfigureAwait(false);

        return updatedNextHeartbeatAt;
    }

    private Task WriteStartupStatusAsync(CancellationToken stoppingToken)
    {
        return WriteLocalStatusAsync(
            new AgentLocalStatusUpdate
            {
                ApiBaseUrl = apiOptions.BaseUrl,
                InstallationId = environment.InstallationId,
                Version = environment.Version,
            },
            stoppingToken);
    }

    private Task WriteActivatedStatusAsync(AgentCredentials credentials, CancellationToken stoppingToken)
    {
        return WriteLocalStatusAsync(
            new AgentLocalStatusUpdate
            {
                AgentId = credentials.AgentId.ToString("D"),
                ApiBaseUrl = apiOptions.BaseUrl,
                Activated = true,
            },
            stoppingToken);
    }

    private async Task<DateTimeOffset> SendHeartbeatIfNeededAsync(AgentCredentials credentials, DateTimeOffset nextHeartbeatAt, CancellationToken stoppingToken)
    {
        if (DateTimeOffset.UtcNow < nextHeartbeatAt)
        {
            return nextHeartbeatAt;
        }

        await heartbeatService.SendAsync(credentials, stoppingToken).ConfigureAwait(false);
        DateTimeOffset heartbeatAt = DateTimeOffset.UtcNow;
        await WriteLocalStatusAsync(
            new AgentLocalStatusUpdate
            {
                AgentId = credentials.AgentId.ToString("D"),
                LastHeartbeatAt = heartbeatAt,
            },
            stoppingToken).ConfigureAwait(false);

        return heartbeatAt.AddSeconds(options.HeartbeatIntervalSeconds);
    }

    private async Task PollCommandsAsync(AgentCredentials credentials, CancellationToken stoppingToken)
    {
        int processed = await pollingService.PollAndExecuteOnceAsync(credentials, stoppingToken).ConfigureAwait(false);
        await WriteLocalStatusAsync(
            new AgentLocalStatusUpdate
            {
                AgentId = credentials.AgentId.ToString("D"),
                LastPollAt = DateTimeOffset.UtcNow,
            },
            stoppingToken).ConfigureAwait(false);

        LogPollingCompleted(logger, processed, null);
    }

    private async Task WriteLocalStatusAsync(AgentLocalStatusUpdate update, CancellationToken cancellationToken)
    {
        try
        {
            await localStatusService.WriteStatusAsync(update, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException)
        {
            LogUnableToWriteLocalStatus(logger, exception);
        }
    }
}
