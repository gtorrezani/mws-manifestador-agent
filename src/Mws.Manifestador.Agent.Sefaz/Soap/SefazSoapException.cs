namespace Mws.Manifestador.Agent.Sefaz.Soap;

#pragma warning disable RCS1194
public sealed class SefazSoapException : HttpRequestException
{
    public SefazSoapException()
    {
        ResponseBody = string.Empty;
    }

    public SefazSoapException(string message)
        : base(message)
    {
        ResponseBody = string.Empty;
    }

    public SefazSoapException(string message, Exception innerException)
        : base(message, innerException)
    {
        ResponseBody = string.Empty;
    }

    public SefazSoapException(int statusCode, string responseBody, string message)
        : base(message)
    {
        StatusCodeNumber = statusCode;
        ResponseBody = responseBody;
    }

    public int StatusCodeNumber { get; }

    public string ResponseBody { get; }
}
#pragma warning restore RCS1194
