using Mws.Manifestador.Agent.Domain.Exceptions;

namespace Mws.Manifestador.Agent.Domain.ValueObjects;

public readonly record struct AccessKey
{
    public AccessKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidAccessKeyException("NF-e access key is required.");
        }

        string normalized = value.Trim();
        if (normalized.Length != 44 || normalized.Any(static c => !char.IsDigit(c)))
        {
            throw new InvalidAccessKeyException("NF-e access key must have exactly 44 numeric characters.");
        }

        Value = normalized;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
