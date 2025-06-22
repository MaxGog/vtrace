using System.Net;
using System.Net.Sockets;
using System.Text;
using vtrace.Models;
using vtrace.Services;

namespace vtrace.Handlers;

internal class VlessProtocolHandler
{
    private const byte VLESS_VERSION = 0x01;
    private const byte VLESS_CMD_TCP = 0x01;
    private const byte VLESS_CMD_UDP = 0x02;
    private const byte VLESS_OPTION_CHUNK = 0x01;
    private const byte VLESS_OPTION_VISION = 0x02;
    
    public async Task PerformHandshake(NetworkConnectionManager connectionManager, VlessConfig config)
    {
        using var handshakeStream = new MemoryStream();
        await using var writer = new BinaryWriter(handshakeStream);
        
        writer.Write(VLESS_VERSION);
        
        if (!Guid.TryParseExact(config.Id, "D", out var uuid))
            throw new ArgumentException("Invalid UUID format");
        writer.Write(uuid.ToByteArray());
        
        byte options = 0;
        if (config.Flow == "xtls-rprx-vision")
            options |= VLESS_OPTION_VISION;
        if (config.EnableChunkStreaming)
            options |= VLESS_OPTION_CHUNK;
        writer.Write(options);
        
        writer.Write(VLESS_CMD_TCP);
        
        if (IPAddress.TryParse(config.Address, out var ip))
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                writer.Write((byte)0x01);
                writer.Write(ip.GetAddressBytes());
            }
            else
            {
                writer.Write((byte)0x02);
                writer.Write(ip.GetAddressBytes());
            }
        }
        else
        {
            writer.Write((byte)0x03);
            var domain = Encoding.UTF8.GetBytes(config.Address);
            if (domain.Length > 255)
                throw new ArgumentException("Domain name too long");
            writer.Write((byte)domain.Length);
            writer.Write(domain);
        }
        
        writer.Write((ushort)IPAddress.HostToNetworkOrder((short)config.Port));
        
        var handshakeData = handshakeStream.ToArray();
        await connectionManager.SendAsync(handshakeData, 0, handshakeData.Length);
        
        var response = new byte[1];
        int bytesRead = await connectionManager.ReceiveAsync(response, 0, 1);
        
        if (bytesRead == 0)
            throw new Exception("Server closed connection immediately");
            
        if (response[0] != 0x00)
            throw new Exception($"Server rejected handshake (code: 0x{response[0]:X2})");
    }
}