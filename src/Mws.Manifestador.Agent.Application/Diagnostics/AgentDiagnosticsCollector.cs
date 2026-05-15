using System.Diagnostics;
using Microsoft.Extensions.Options;
using Mws.Manifestador.Agent.Application.Configuration;
using Mws.Manifestador.Agent.Application.Interfaces;

namespace Mws.Manifestador.Agent.Application.Diagnostics;

public sealed class AgentDiagnosticsCollector
{
    private readonly AgentApiOptions apiOptions;
    private readonly IAgentEnvironment environment;
    private readonly ICertificateProvider certificateProvider;

    public AgentDiagnosticsCollector(
        IAgentEnvironment environment,
        ICertificateProvider certificateProvider,
        IOptions<AgentApiOptions> apiOptions)
    {
        this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
        this.certificateProvider = certificateProvider ?? throw new ArgumentNullException(nameof(certificateProvider));
        this.apiOptions = apiOptions?.Value ?? throw new ArgumentNullException(nameof(apiOptions));
    }

    public async Task<AgentDiagnosticsSnapshot> CollectAsync(CancellationToken cancellationToken)
    {
        int certificateCount = 0;
        string storeAccessStatus = "accessible";
        string? storeAccessErrorCode = null;

        try
        {
            certificateCount = (await certificateProvider.ListAsync(cancellationToken).ConfigureAwait(false)).Count;
        }
        catch (UnauthorizedAccessException)
        {
            storeAccessStatus = "failed";
            storeAccessErrorCode = "CERTIFICATE_STORE_ACCESS_DENIED";
        }
        catch (InvalidOperationException)
        {
            storeAccessStatus = "failed";
            storeAccessErrorCode = "CERTIFICATE_STORE_INVALID_OPERATION";
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            storeAccessStatus = "failed";
            storeAccessErrorCode = "CERTIFICATE_STORE_CRYPTOGRAPHIC_ERROR";
        }

        return new AgentDiagnosticsSnapshot(
            environment.Version,
            environment.MachineName,
            environment.InstallationId,
            GetUptimeSeconds(),
            SanitizeBaseUrl(apiOptions.BaseUrl),
            certificateCount,
            storeAccessStatus,
            storeAccessErrorCode,
            Environment.OSVersion.VersionString,
            CurrentProcessUser(),
            ExecutionMode());
    }

    private static long GetUptimeSeconds()
    {
        try
        {
            DateTimeOffset startedAt = new(Process.GetCurrentProcess().StartTime);

            return Math.Max(0, (long)(DateTimeOffset.Now - startedAt).TotalSeconds);
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }

    private static Uri SanitizeBaseUrl(Uri value)
    {
        UriBuilder builder = new(value)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Path = string.Empty,
            Query = string.Empty,
            Fragment = string.Empty,
        };

        return new Uri(builder.Uri.GetLeftPart(UriPartial.Authority));
    }

    private static string CurrentProcessUser()
    {
        string domain = Environment.UserDomainName;
        string user = Environment.UserName;

        return string.IsNullOrWhiteSpace(domain) ? user : $"{domain}\\{user}";
    }

    private static string ExecutionMode()
    {
        if (OperatingSystem.IsWindows() && !Environment.UserInteractive)
        {
            return "windows_service";
        }

        return "console";
    }
}
