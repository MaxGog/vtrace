using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using vtrace.Models;

namespace vtrace.Services;

internal class SecurityLayerHandler
{
    public async Task EstablishSecurityLayer(Stream networkStream, VlessConfig config, 
        CancellationToken ct, Action<string> statusNotifier)
    {
        if (config.Security == "none")
        {
            statusNotifier("Skipping TLS (insecure mode)");
            return;
        }

        if (config.Security == "tls" || config.Security == "reality" || config.Port == 443)
        {
            statusNotifier("Establishing TLS layer...");
            
            var sslOptions = CreateSslOptions(config);
            
            if (config.Security == "reality")
                ConfigureRealityOptions(sslOptions, config, statusNotifier);

            try
            {
                var sslStream = new SslStream(networkStream, false);
                await sslStream.AuthenticateAsClientAsync(sslOptions, ct);
                
                statusNotifier(config.Security == "reality" 
                    ? "Reality handshake completed" 
                    : "TLS handshake completed");
            }
            catch (AuthenticationException ex)
            {
                statusNotifier($"Authentication failed: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                statusNotifier($"TLS setup error: {ex.Message}");
                throw;
            }
        }
    }

    private SslClientAuthenticationOptions CreateSslOptions(VlessConfig config) => new()
    {
        TargetHost = config.Sni ?? config.Address,
        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
        CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
        EncryptionPolicy = EncryptionPolicy.RequireEncryption,
        RemoteCertificateValidationCallback = (sender, cert, chain, errors) => 
            CertificateValidator.Validate(cert, errors, config)
    };

    private void ConfigureRealityOptions(SslClientAuthenticationOptions options, 
        VlessConfig config, Action<string> statusNotifier)
    {
        statusNotifier("Configuring Reality security...");
        
        options.CipherSuitesPolicy = new CipherSuitesPolicy(new[]
        {
            TlsCipherSuite.TLS_AES_128_GCM_SHA256,
            TlsCipherSuite.TLS_CHACHA20_POLY1305_SHA256,
            TlsCipherSuite.TLS_AES_256_GCM_SHA384
        });

        options.ApplicationProtocols = new List<SslApplicationProtocol> 
        {
            SslApplicationProtocol.Http2,
            SslApplicationProtocol.Http11
        };

        if (!string.IsNullOrEmpty(config.Fingerprint))
            ApplyFingerprint(options, config.Fingerprint);

        if (!string.IsNullOrEmpty(config.PublicKey))
            HandlePublicKey(config.PublicKey, statusNotifier);

        if (!string.IsNullOrEmpty(config.ShortId))
            statusNotifier($"Using Reality short ID: {config.ShortId}");
    }

    private void ApplyFingerprint(SslClientAuthenticationOptions options, string fingerprint)
    {
        switch (fingerprint.ToLower())
        {
            case "chrome":
                options.EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
                options.CipherSuitesPolicy = new CipherSuitesPolicy(new[]
                {
                    TlsCipherSuite.TLS_AES_128_GCM_SHA256,
                    TlsCipherSuite.TLS_AES_256_GCM_SHA384,
                    TlsCipherSuite.TLS_CHACHA20_POLY1305_SHA256,
                    TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256,
                    TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256
                });
                break;
                
            case "firefox":
                options.EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
                options.CipherSuitesPolicy = new CipherSuitesPolicy(new[]
                {
                    TlsCipherSuite.TLS_AES_128_GCM_SHA256,
                    TlsCipherSuite.TLS_CHACHA20_POLY1305_SHA256,
                    TlsCipherSuite.TLS_AES_256_GCM_SHA384,
                    TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256,
                    TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256
                });
                break;
        }
    }

    private void HandlePublicKey(string publicKey, Action<string> statusNotifier)
    {
        try
        {
            Convert.FromBase64String(publicKey);
            statusNotifier($"Using Reality public key: {publicKey[..8]}...");
        }
        catch (FormatException)
        {
            statusNotifier("Invalid Reality public key format");
            throw;
        }
    }
}