using vtrace.Models;

namespace vtrace.Interfaces;

public interface IVlessVpnService : IDisposable
{
    long BytesReceived { get; }
    long BytesSent { get; }
    bool IsConnected { get; }
    TimeSpan Uptime { get; }
    
    event EventHandler<string>? ConnectionStatusChanged;
    event EventHandler<long>? DataTransferred;
    
    Task<bool> Connect(VlessConfig config);
    Task<int> SendAsync(byte[] buffer, int offset, int count);
    Task<int> ReceiveAsync(byte[] buffer, int offset, int count);
    void Disconnect();
}