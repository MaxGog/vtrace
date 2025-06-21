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

    private long _bytesReceived;
    private long _bytesSent;

    public long BytesReceived => _bytesReceived;
    public long BytesSent => _bytesSent;

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

            var connectTask = _tcpClient.ConnectAsync(config.Address, config.Port);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            
            var completedTask = await Task.WhenAny(connectTask, timeoutTask);
            if (completedTask == timeoutTask)
            {
                throw new TimeoutException("Connection timeout");
            }
            
            if (!_tcpClient.Connected)
            {
                throw new Exception("Connection failed without exception");
            }

            _networkStream = _tcpClient.GetStream();

            await SendVlessHandshake(config);
            
            _isConnected = true;
            OnConnectionStatusChanged("Connected");
            
            _ = Task.Run(() => ReadDataAsync(_cancellationTokenSource.Token));
            
            return true;
        }
        catch (Exception ex)
        {
            string errorMessage = ex switch
            {
                TimeoutException => "Connection timeout (server not responding)",
                SocketException sockEx => sockEx.SocketErrorCode switch
                {
                    SocketError.ConnectionRefused => "Connection refused (server is down)",
                    SocketError.HostUnreachable => "Host unreachable (check network)",
                    SocketError.NetworkUnreachable => "Network unavailable",
                    _ => $"Network error: {sockEx.SocketErrorCode}"
                },
                _ => $"Connection error: {ex.Message}"
            };
            
            OnConnectionStatusChanged(errorMessage);
            Disconnect();
            return false;
        }
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
                    Disconnect();
                    break;
                }
                
                _bytesReceived += bytesRead;
                
                // Process received data...
            }
        }
        catch
        {
            Disconnect();
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

            // Не перезаписываем статус, если уже есть сообщение об ошибке
            if (!_cancellationTokenSource?.IsCancellationRequested ?? false)
            {
                OnConnectionStatusChanged("Disconnected");
            }
        }
        catch
        {
            // Игонорируем ошибки
        }
    }

    private async Task SendVlessHandshake(VlessConfig config)
    {
        var handshakeData = new List<byte>
        {
            1
        };
        
        var idBytes = System.Text.Encoding.UTF8.GetBytes(config.Id);
        handshakeData.AddRange(idBytes);

        if (!string.IsNullOrEmpty(config.Flow))
        {
            var flowBytes = System.Text.Encoding.UTF8.GetBytes(config.Flow);
            handshakeData.AddRange(flowBytes);
        }
        
        await _networkStream.WriteAsync(handshakeData.ToArray().AsMemory(0, handshakeData.Count));
    }

    private void OnConnectionStatusChanged(string status)
    {
        ConnectionStatusChanged?.Invoke(this, status);
    }
}