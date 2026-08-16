using System.Security.Cryptography;
using System.Text;

namespace HookFlow.Services;

public interface ISignatureService
{
    string ComputeHmacSha256(string payload, string secret);
}

public class SignatureService : ISignatureService
{
    public string ComputeHmacSha256(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
