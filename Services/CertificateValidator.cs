using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using vtrace.Models;

namespace vtrace.Services;

internal static class CertificateValidator
{
    public static bool Validate(X509Certificate? cert, SslPolicyErrors errors, VlessConfig config)
    {
        if (cert == null) return false;

        if (config.Security == "reality")
        {
            if (string.IsNullOrEmpty(config.Fingerprint))
                return false;

            try
            {
                var fingerprint = config.Fingerprint
                    .Replace(":", "").Replace("-", "").Replace(" ", "").ToLowerInvariant();

                using var sha256 = SHA256.Create();
                var certHash = sha256.ComputeHash(cert.GetRawCertData());
                var certHashHex = BitConverter.ToString(certHash)
                    .Replace("-", "").ToLowerInvariant();

                return string.Equals(fingerprint, certHashHex, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        return errors == SslPolicyErrors.None || 
               errors == SslPolicyErrors.RemoteCertificateNameMismatch;
    }

    public static byte[] HexStringToByteArray(string hex)
    {
        hex = hex.Replace(":", "").Replace("-", "").Replace(" ", "");

        if (hex.Length % 2 != 0)
            throw new ArgumentException("Hex string must have even length after cleaning");

        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            var hexByte = hex.Substring(i * 2, 2);
            if (!byte.TryParse(hexByte, System.Globalization.NumberStyles.HexNumber, null, out bytes[i]))
                throw new ArgumentException($"Invalid hex byte: {hexByte}");
        }
        return bytes;
    }
}