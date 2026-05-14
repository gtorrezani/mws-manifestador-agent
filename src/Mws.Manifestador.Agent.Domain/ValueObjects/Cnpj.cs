using Mws.Manifestador.Agent.Domain.Exceptions;

namespace Mws.Manifestador.Agent.Domain.ValueObjects;

public readonly record struct Cnpj
{
    public Cnpj(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("CNPJ is required.");
        }

        string normalized = new(value.Where(char.IsDigit).ToArray());
        if (normalized.Length != 14)
        {
            throw new DomainException("CNPJ must have exactly 14 numeric characters.");
        }

        Value = normalized;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
