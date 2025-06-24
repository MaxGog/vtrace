using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using vtrace.Models;

namespace vtrace.Services;

internal static class CertificateValidator
{
    public static bool Validate(X509Certificate? cert, SslPolicyErrors errors, VlessConfig config)
    {
        if (cert == null) 
        {
            Console.WriteLine("Certificate is null");
            return false;
        }

        Console.WriteLine($"Validating cert for {config.Security}. Errors: {errors}");
        Console.WriteLine($"Cert subject: {cert.Subject}, issuer: {cert.Issuer}");

        if (config.Security == "reality")
        {
            if (string.IsNullOrEmpty(config.Fingerprint))
            {
                Console.WriteLine("No fingerprint provided for REALITY validation");
                return false;
            }

            try
            {
                var fingerprint = config.Fingerprint
                    .Replace(":", "").Replace("-", "").Replace(" ", "").ToLowerInvariant();

                using var publicKey = GetPublicKey(cert);
                using var sha256 = SHA256.Create();
                var publicKeyBytes = publicKey.ExportSubjectPublicKeyInfo();
                var certHash = sha256.ComputeHash(publicKeyBytes);
                var certHashHex = BitConverter.ToString(certHash)
                    .Replace("-", "").ToLowerInvariant();

                Console.WriteLine($"Expected fingerprint: {fingerprint}");
                Console.WriteLine($"Actual fingerprint: {certHashHex}");

                return string.Equals(fingerprint, certHashHex, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) 
            { 
                Console.WriteLine($"Certificate validation error: {ex}");
                return false; 
            }
        }

        bool isValid = errors == SslPolicyErrors.None || 
                      errors == SslPolicyErrors.RemoteCertificateNameMismatch;
        
        if (!isValid)
        {
            Console.WriteLine($"Certificate validation failed with errors: {errors}");
        }
        
        return isValid;
    }


    private static RSA GetPublicKey(X509Certificate cert)
    {
        if (cert is X509Certificate2 cert2)
        {
            return cert2.GetRSAPublicKey() ?? throw new Exception("Not an RSA certificate");
        }

        using var cert2FromCert = new X509Certificate2(cert);
        return cert2FromCert.GetRSAPublicKey() ?? throw new Exception("Not an RSA certificate");
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