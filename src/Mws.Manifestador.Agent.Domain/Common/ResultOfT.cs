namespace Mws.Manifestador.Agent.Domain.Common;

public sealed record Result<T>
{
    internal Result(T? value, string? errorCode, string? errorMessage)
    {
        Value = value;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess => ErrorCode is null;

    public T? Value { get; }

    public string? ErrorCode { get; }

    public string? ErrorMessage { get; }
}
