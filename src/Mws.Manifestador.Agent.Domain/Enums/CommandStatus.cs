namespace Mws.Manifestador.Agent.Domain.Enums;

public enum CommandStatus
{
    Pending,
    Locked,
    Processing,
    Completed,
    Failed,
    Cancelled,
    Expired,
}
