using System.Security.Cryptography;
using System.Text;

namespace VeggieAlly.Infrastructure.Line;

public static class LineSignatureValidator
{
    public static bool Validate(string channelSecret, byte[] requestBody, string signature)
    {
        if (string.IsNullOrWhiteSpace(channelSecret) || 
            string.IsNullOrWhiteSpace(signature) || 
            requestBody.Length == 0)
        {
            return false;
        }

        try
        {
            var keyBytes = Encoding.UTF8.GetBytes(channelSecret);
            using var hmac = new HMACSHA256(keyBytes);
            var computedHash = hmac.ComputeHash(requestBody);
            var computedBase64 = Convert.ToBase64String(computedHash);

            // 常數時間比對（CryptographicOperations.FixedTimeEquals）防止時序攻擊
            var providedBytes = Encoding.UTF8.GetBytes(signature);
            var computedBytes = Encoding.UTF8.GetBytes(computedBase64);

            return CryptographicOperations.FixedTimeEquals(providedBytes, computedBytes);
        }
        catch
        {
            return false;
        }
    }
}