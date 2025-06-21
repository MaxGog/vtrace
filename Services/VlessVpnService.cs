using System.Net.Sockets;

using vtrace.Models;
using vtrace.Interfaces;


namespace vtrace.Services;

public class VlessVpnService : IVlessVpnService
{
    private TcpClient? _tcpClient;
    private NetworkStream? _networkStream;
    private bool _isConnected = false;
    private CancellationTokenSource? _cancellationTokenSource;

    public bool IsConnected => _isConnected;

    public event EventHandler<string> ConnectionStatusChanged;

    public async Task<bool> Connect(VlessConfig config)
    {
        if (_isConnected)
        {
            Disconnect();
        }

        try
        {
            OnConnectionStatusChanged("Connecting...");
            
            _tcpClient = new TcpClient();
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Connect to the server
            await _tcpClient.ConnectAsync(config.Address, config.Port, _cancellationTokenSource.Token);
            
            if (!_tcpClient.Connected)
            {
                OnConnectionStatusChanged("Connection failed");
                return false;
            }

            _networkStream = _tcpClient.GetStream();
            
            // Send VLESS protocol handshake (simplified version without encryption/TLS)
            await SendVlessHandshake(config);
            
            _isConnected = true;
            OnConnectionStatusChanged("Connected");
            
            // Start reading responses in background
            _ = Task.Run(() => ReadDataAsync(_cancellationTokenSource.Token));
            
            return true;
        }
        catch (Exception ex)
        {
            OnConnectionStatusChanged($"Connection error: {ex.Message}");
            Disconnect();
            return false;
        }
    }

    public void Disconnect()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
            
            _networkStream?.Close();
            _networkStream?.Dispose();
            _networkStream = null;
            
            _tcpClient?.Close();
            _tcpClient?.Dispose();
            _tcpClient = null;
            
            _isConnected = false;
            OnConnectionStatusChanged("Disconnected");
        }
        catch
        {
            // Ignore disposal errors
        }
    }

    private async Task SendVlessHandshake(VlessConfig config)
    {
        var handshakeData = new List<byte>
        {
            1
        };
        
        // Add UUID (ID)
        var idBytes = System.Text.Encoding.UTF8.GetBytes(config.Id);
        handshakeData.AddRange(idBytes);
        
        // Add additional parameters if needed
        if (!string.IsNullOrEmpty(config.Flow))
        {
            var flowBytes = System.Text.Encoding.UTF8.GetBytes(config.Flow);
            handshakeData.AddRange(flowBytes);
        }
        
        await _networkStream.WriteAsync(handshakeData.ToArray().AsMemory(0, handshakeData.Count));
    }

    private async Task ReadDataAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        
        try
        {
            while (!cancellationToken.IsCancellationRequested && _tcpClient.Connected)
            {
                var bytesRead = await _networkStream.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0)
                {
                    // Connection closed by server
                    Disconnect();
                    break;
                }
                
                // Process received data here
                // In a real implementation, you would handle the VPN packets
            }
        }
        catch (Exception ex)
        {
            OnConnectionStatusChanged($"Read error: {ex.Message}");
            Disconnect();
        }
    }

    private void OnConnectionStatusChanged(string status)
    {
        ConnectionStatusChanged?.Invoke(this, status);
    }
}