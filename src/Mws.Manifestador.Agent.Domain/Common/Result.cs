namespace Mws.Manifestador.Agent.Domain.Common;

public static class Result
{
    public static Result<T> Success<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(value, null, null);
    }

    public static Result<T> Failure<T>(string errorCode, string errorMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
        return new Result<T>(default, errorCode, errorMessage);
    }
}
