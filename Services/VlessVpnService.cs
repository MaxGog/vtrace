using System.Net.Sockets;
using vtrace.Handlers;
using vtrace.Interfaces;
using vtrace.Models;

namespace vtrace.Services;

public sealed partial class VlessVpnService : IVlessVpnService, IDisposable
{
    private readonly NetworkConnectionManager _connectionManager;
    private readonly VlessProtocolHandler _protocolHandler;
    
    public long BytesReceived => _connectionManager.BytesReceived;
    public long BytesSent => _connectionManager.BytesSent;
    public bool IsConnected => _connectionManager.IsConnected;
    public TimeSpan Uptime => _connectionManager.Uptime;
    
    public event EventHandler<string>? ConnectionStatusChanged;
    public event EventHandler<long>? DataTransferred;

    public VlessVpnService()
    {
        _connectionManager = new NetworkConnectionManager();
        _protocolHandler = new VlessProtocolHandler();
        
        _connectionManager.ConnectionStatusChanged += (s, e) => ConnectionStatusChanged?.Invoke(s, e);
        _connectionManager.DataTransferred += (s, e) => DataTransferred?.Invoke(s, e);
    }

    public async Task<bool> Connect(VlessConfig config)
    {
        if (IsConnected) Disconnect();
        
        try
        {
            await _connectionManager.EstablishConnection(config);
            await _protocolHandler.PerformHandshake(_connectionManager, config);
            return true;
        }
        catch (Exception ex)
        {
            Disconnect();
            throw new Exception("Connection failed", ex);
        }
    }

    public Task<int> SendAsync(byte[] buffer, int offset, int count) 
        => _connectionManager.SendAsync(buffer, offset, count);

    public Task<int> ReceiveAsync(byte[] buffer, int offset, int count) 
        => _connectionManager.ReceiveAsync(buffer, offset, count);

    public void Disconnect() => _connectionManager.Disconnect();

    public void Dispose() => Disconnect();
}