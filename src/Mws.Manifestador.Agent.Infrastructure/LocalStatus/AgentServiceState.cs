namespace Mws.Manifestador.Agent.Infrastructure.LocalStatus;

public enum AgentServiceState
{
    Unknown,
    NotInstalled,
    Stopped,
    StartPending,
    StopPending,
    Running,
    Paused,
}
