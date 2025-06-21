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
    public string PublicKey { get; set; }
    public string Fingerprint { get; set; }
    public string Sni { get; set; }
    public string Sid { get; set; }
    public string Spx { get; set; }
    public string Flow { get; set; }
    public string Remark { get; set; }

    public static VlessConfig Parse(string vlessUrl)
    {
        if (!vlessUrl.StartsWith("vless://"))
            throw new ArgumentException("Invalid VLESS URL");

        var uri = new Uri(vlessUrl);
        var config = new VlessConfig
        {
            Id = uri.UserInfo.Split('@')[0],
            Address = uri.Host,
            Port = uri.Port,
            Remark = uri.Fragment.TrimStart('#')
        };

        var query = HttpUtility.ParseQueryString(uri.Query);
        config.Type = query["type"];
        config.Security = query["security"];
        config.PublicKey = query["pbk"];
        config.Fingerprint = query["fp"];
        config.Sni = query["sni"];
        config.Sid = query["sid"];
        config.Spx = query["spx"];
        config.Flow = query["flow"];

        return config;
    }
}