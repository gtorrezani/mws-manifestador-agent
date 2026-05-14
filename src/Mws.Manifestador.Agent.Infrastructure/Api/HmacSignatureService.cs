using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Mws.Manifestador.Agent.Infrastructure.Api;

public static class HmacSignatureService
{
    public static HmacSignedRequest Sign(string secret, HttpMethod method, string pathAndQuery, string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentNullException.ThrowIfNull(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(pathAndQuery);
        ArgumentNullException.ThrowIfNull(body);

        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        string nonce = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        string bodyHash = Sha256Hex(body);
        string canonical = string.Join('\n', method.Method.ToUpperInvariant(), pathAndQuery, timestamp, nonce, bodyHash);
        string signature = HmacSha256Hex(secret, canonical);

        return new HmacSignedRequest(timestamp, nonce, bodyHash, signature);
    }

    public static string Sha256Hex(string value)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToUpperInvariant();
    }

    private static string HmacSha256Hex(string secret, string canonical)
    {
        using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(secret));
        byte[] bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToUpperInvariant();
    }
}
