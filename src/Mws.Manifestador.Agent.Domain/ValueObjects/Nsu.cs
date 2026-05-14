using Mws.Manifestador.Agent.Domain.Exceptions;

namespace Mws.Manifestador.Agent.Domain.ValueObjects;

public readonly record struct Nsu
{
    public Nsu(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(static c => !char.IsDigit(c)))
        {
            throw new DomainException("NSU must be numeric.");
        }

        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}
