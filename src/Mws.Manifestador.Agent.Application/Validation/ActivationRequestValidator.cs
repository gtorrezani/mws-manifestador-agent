using FluentValidation;
using Mws.Manifestador.Agent.Application.DTOs;

namespace Mws.Manifestador.Agent.Application.Validation;

public sealed class ActivationRequestValidator : AbstractValidator<ActivationRequest>
{
    public ActivationRequestValidator()
    {
        RuleFor(static request => request.ActivationCode).NotEmpty().MaximumLength(64);
        RuleFor(static request => request.InstallationId).NotEmpty().MaximumLength(120);
        RuleFor(static request => request.MachineName).NotEmpty().MaximumLength(120);
        RuleFor(static request => request.Version).NotEmpty().MaximumLength(40);
    }
}
