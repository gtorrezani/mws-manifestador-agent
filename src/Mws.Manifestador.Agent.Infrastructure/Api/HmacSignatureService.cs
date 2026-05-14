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

        return Sign(secret, method, pathAndQuery, body, timestamp, nonce);
    }

    public static HmacSignedRequest Sign(string secret, HttpMethod method, string pathAndQuery, string body, string timestamp, string nonce)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentNullException.ThrowIfNull(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(pathAndQuery);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(timestamp);
        ArgumentException.ThrowIfNullOrWhiteSpace(nonce);

        string bodyHash = Sha256Hex(body);
        string canonical = string.Join('\n', method.Method.ToUpperInvariant(), pathAndQuery, timestamp, nonce, bodyHash);
        string signature = HmacSha256Hex(secret, canonical);

        return new HmacSignedRequest(timestamp, nonce, bodyHash, signature);
    }

    public static string Sha256Hex(string value)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return ToLowerHex(bytes);
    }

    private static string HmacSha256Hex(string secret, string canonical)
    {
        using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(secret));
        byte[] bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        return ToLowerHex(bytes);
    }

    private static string ToLowerHex(byte[] bytes)
    {
        const string Hex = "0123456789abcdef";

        return string.Create(
            bytes.Length * 2,
            bytes,
            static (chars, state) =>
            {
                for (int i = 0; i < state.Length; i++)
                {
                    byte value = state[i];
                    chars[i * 2] = Hex[value >> 4];
                    chars[(i * 2) + 1] = Hex[value & 0x0F];
                }
            });
    }
}
