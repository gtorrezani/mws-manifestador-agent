namespace Mws.Manifestador.Agent.Application.Configuration;

public sealed class AgentPollingOptions
{
    public const string SectionName = "AgentPolling";

    public int IntervalSeconds { get; init; } = 30;

    public int HeartbeatIntervalSeconds { get; init; } = 60;

    public int MaxCommandsPerPoll { get; init; } = 5;
}
