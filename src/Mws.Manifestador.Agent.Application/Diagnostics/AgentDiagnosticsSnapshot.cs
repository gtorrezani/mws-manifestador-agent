namespace Mws.Manifestador.Agent.Application.Diagnostics;

public sealed record AgentDiagnosticsSnapshot(
    string Version,
    string MachineName,
    string InstallationId,
    long ServiceUptimeSeconds,
    Uri ApiBaseUrl,
    int CertificateInventoryCount,
    string StoreAccessStatus,
    string? StoreAccessErrorCode,
    string OsVersion,
    string CurrentProcessUser,
    string ExecutionMode);
