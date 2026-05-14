using FluentValidation;
using Microsoft.Extensions.Options;
using Mws.Manifestador.Agent.Application.Configuration;
using Mws.Manifestador.Agent.Application.DTOs;
using Mws.Manifestador.Agent.Application.Interfaces;

namespace Mws.Manifestador.Agent.Application.Services;

public sealed class AgentActivationService
{
    private readonly IAgentApiClient apiClient;
    private readonly IAgentCredentialStore credentialStore;
    private readonly IAgentEnvironment environment;
    private readonly ICertificateStore certificateStore;
    private readonly IValidator<ActivationRequest> validator;
    private readonly AgentApiOptions options;

    public AgentActivationService(
        IAgentApiClient apiClient,
        IAgentCredentialStore credentialStore,
        IAgentEnvironment environment,
        ICertificateStore certificateStore,
        IValidator<ActivationRequest> validator,
        IOptions<AgentApiOptions> options)
    {
        this.apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        this.credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
        this.certificateStore = certificateStore ?? throw new ArgumentNullException(nameof(certificateStore));
        this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<AgentCredentials?> EnsureActivatedAsync(CancellationToken cancellationToken)
    {
        AgentCredentials? credentials = await credentialStore.GetAsync(cancellationToken).ConfigureAwait(false);
        if (credentials is not null)
        {
            return credentials;
        }

        if (string.IsNullOrWhiteSpace(options.ActivationCode))
        {
            return null;
        }

        IReadOnlyCollection<CertificateInfo> certificates = await certificateStore.ListAsync(cancellationToken).ConfigureAwait(false);
        ActivationRequest request = new(
            options.ActivationCode,
            environment.InstallationId,
            environment.MachineName,
            environment.Version,
            certificates);

        await validator.ValidateAndThrowAsync(request, cancellationToken).ConfigureAwait(false);
        ActivationResponse response = await apiClient.ActivateAsync(request, cancellationToken).ConfigureAwait(false);
        AgentCredentials activated = new(response.AgentId, response.Secret);
        await credentialStore.SaveAsync(activated, cancellationToken).ConfigureAwait(false);

        return activated;
    }
}
