using vtrace.Models;

namespace vtrace.Interfaces;

public interface IVlessVpnService
{
    bool IsConnected { get; }
    long BytesReceived { get; }
    long BytesSent { get; }
    event EventHandler<string> ConnectionStatusChanged;
    
    Task<bool> Connect(VlessConfig config);
    void Disconnect();
}