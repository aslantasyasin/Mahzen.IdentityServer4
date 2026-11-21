using System;
using System.Security.Cryptography;
using System.Text;

namespace IdentityServer.Services.Login;

public static class LoginSecrets
{
    private const int DeviceTokenBytes = 32;
    private const int CodeDigits = 6;

    public static string NewDeviceToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(DeviceTokenBytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string NewCode() =>
        RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, CodeDigits)).ToString("D" + CodeDigits);

    public static string HashDeviceToken(string token) => Sha256Hex(token);

    public static string HashCode(Guid challengeId, string code) => Sha256Hex(challengeId.ToString("N") + ":" + code);

    public static bool HashesMatch(string expectedHex, string actualHex) =>
        expectedHex is not null && actualHex is not null &&
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expectedHex), Encoding.ASCII.GetBytes(actualHex));

    private static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
