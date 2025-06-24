using System;
using System.Web;

namespace vtrace.Models;

public class VlessConfig
{
    public string Id { get; set; }
    public string Address { get; set; }
    public int Port { get; set; }
    public string Type { get; set; }
    public string Security { get; set; }
    public string? PublicKey { get; set; }
    public string? Fingerprint { get; set; }
    public string? Sni { get; set; }
    public string? ShortId { get; set; }
    public string? SpiderX { get; set; }
    public string? Flow { get; set; }
    public string? Remark { get; set; }

    /*public static VlessConfig Parse(string vlessUrl)
    {
        if (string.IsNullOrWhiteSpace(vlessUrl))
            throw new ArgumentException("URL cannot be empty");

        if (!vlessUrl.StartsWith("vless://", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Invalid VLESS URL - must start with 'vless://'");

        try
        {
            var fragmentIndex = vlessUrl.IndexOf('#');
            var fragment = fragmentIndex >= 0 ? vlessUrl.Substring(fragmentIndex + 1) : string.Empty;
            var urlWithoutFragment = fragmentIndex >= 0 ? vlessUrl.Substring(0, fragmentIndex) : vlessUrl;

            var uri = new Uri(urlWithoutFragment);

            var config = new VlessConfig
            {
                Id = !string.IsNullOrEmpty(uri.UserInfo) ? uri.UserInfo.Split('@')[0] : null,
                Address = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 443,
                Remark = Uri.UnescapeDataString(fragment)
            };

            if (!string.IsNullOrEmpty(uri.UserInfo) && uri.UserInfo.Contains('@'))
            {
                var userParts = uri.UserInfo.Split('@');
                if (userParts.Length > 1 && !string.IsNullOrEmpty(userParts[1]))
                {
                    config.Address = userParts[1];
                }
            }

            var query = HttpUtility.ParseQueryString(uri.Query);

            config.Type = query["type"]?.ToLower() ?? "tcp";
            config.Security = query["security"]?.ToLower() ?? (config.Port == 443 ? "tls" : "none");
            config.PublicKey = query["pbk"] ?? query["publicKey"];
            config.Fingerprint = query["fp"] ?? query["fingerprint"] ?? "chrome";
            config.Sni = query["sni"] ?? query["serverName"];
            config.Sid = query["sid"] ?? query["shortId"];
            config.Spx = query["spx"] ?? query["spiderX"];
            config.Flow = query["flow"];

            if (string.IsNullOrEmpty(config.Address))
                throw new ArgumentException("Address is required");

            if (config.Security == "reality" && string.IsNullOrEmpty(config.PublicKey))
                throw new ArgumentException("Public key (pbk) is required for reality security");

            return config;
        }
        catch (UriFormatException ex)
        {
            throw new ArgumentException("Invalid VLESS URL format", ex);
        }
    }*/

    public static VlessConfig Parse(string vlessUrl)
    {
        var uri = new Uri(vlessUrl);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

        return new VlessConfig
        {
            Id = uri.UserInfo,
            Address = uri.Host,
            Port = uri.Port,
            Type = query["type"] ?? "tcp",
            Security = query["security"] ?? "tls",
            PublicKey = query["pbk"],
            Fingerprint = query["fp"],
            Sni = query["sni"],
            ShortId = query["sid"],
            SpiderX = query["spx"],
            Flow = query["flow"],
            Remark = uri.Fragment.TrimStart('#')
        };
    }
}