using System.Net.Sockets;
using vtrace.Interfaces;
using vtrace.Models;

namespace vtrace.Services;

public sealed class VlessVpnService : IVlessVpnService, IDisposable
{
    private const int ConnectionTimeoutSeconds = 5;
    private const int ReadBufferSize = 4096;
    
    private TcpClient? _tcpClient;
    private NetworkStream? _networkStream;
    private CancellationTokenSource? _cts;
    
    private long _bytesReceived;
    private long _bytesSent;
    private volatile bool _isConnected;

    public long BytesReceived => _bytesReceived;
    public long BytesSent => _bytesSent;
    public bool IsConnected => _isConnected;
    
    public event EventHandler<string>? ConnectionStatusChanged;

    public async Task<bool> Connect(VlessConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        
        if (_isConnected)
        {
            Disconnect();
        }

        try
        {
            await EstablishConnectionAsync(config);
            await PerformHandshakeAsync(config);
            
            StartDataReading();
            return true;
        }
        catch (Exception ex)
        {
            HandleConnectionError(ex);
            return false;
        }
    }

    private async Task EstablishConnectionAsync(VlessConfig config)
    {
        NotifyStatus("Connecting...");
        
        _tcpClient = new TcpClient();
        _cts = new CancellationTokenSource();
        
        var connectTask = _tcpClient.ConnectAsync(config.Address, config.Port);
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(ConnectionTimeoutSeconds));
        
        if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
        {
            throw new TimeoutException("Connection timeout");
        }

        if (!_tcpClient.Connected)
        {
            throw new InvalidOperationException("Connection failed without exception");
        }

        _networkStream = _tcpClient.GetStream();
    }

    private async Task PerformHandshakeAsync(VlessConfig config)
    {
        var handshakeData = BuildHandshakeData(config);
        await _networkStream!.WriteAsync(handshakeData, _cts!.Token);
        
        _isConnected = true;
        NotifyStatus("Connected");
    }

    private static byte[] BuildHandshakeData(VlessConfig config)
    {
        var handshake = new List<byte> { 0x01 }; // Protocol version
        
        handshake.AddRange(System.Text.Encoding.UTF8.GetBytes(config.Id));
        
        if (!string.IsNullOrEmpty(config.Flow))
        {
            handshake.AddRange(System.Text.Encoding.UTF8.GetBytes(config.Flow));
        }
        
        return handshake.ToArray();
    }

    private void StartDataReading()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await ReadDataContinuouslyAsync();
            }
            catch
            {
                Disconnect();
            }
        }, _cts!.Token);
    }

    private async Task ReadDataContinuouslyAsync()
    {
        var buffer = new byte[ReadBufferSize];
        
        while (!_cts!.IsCancellationRequested && 
               _tcpClient!.Connected && 
               _networkStream!.CanRead)
        {
            var bytesRead = await _networkStream.ReadAsync(buffer, _cts.Token);
            if (bytesRead == 0) break;
            
            Interlocked.Add(ref _bytesReceived, bytesRead);
            
            // Process received data here...
        }
    }

    private void HandleConnectionError(Exception ex)
    {
        var errorMessage = ex switch
        {
            TimeoutException => "Connection timeout (server not responding)",
            SocketException sockEx => GetSocketErrorMessage(sockEx),
            _ => $"Connection error: {ex.Message}"
        };
        
        NotifyStatus(errorMessage);
        Disconnect();
    }

    private static string GetSocketErrorMessage(SocketException ex) => ex.SocketErrorCode switch
    {
        SocketError.ConnectionRefused => "Connection refused (server is down)",
        SocketError.HostUnreachable => "Host unreachable (check network)",
        SocketError.NetworkUnreachable => "Network unavailable",
        _ => $"Network error: {ex.SocketErrorCode}"
    };

    public void Disconnect()
    {
        try
        {
            if (!_isConnected) return;
            
            _cts?.Cancel();
            
            CleanupNetworkResources();
            
            _isConnected = false;
            
            if (!(_cts?.IsCancellationRequested ?? true))
            {
                NotifyStatus("Disconnected");
            }
        }
        catch
        {
            // Logging can be added here
        }
    }

    private void CleanupNetworkResources()
    {
        _networkStream?.Close();
        _networkStream?.Dispose();
        _networkStream = null;

        _tcpClient?.Close();
        _tcpClient?.Dispose();
        _tcpClient = null;
    }

    private void NotifyStatus(string status)
    {
        ConnectionStatusChanged?.Invoke(this, status);
    }

    public void Dispose()
    {
        Disconnect();
        _cts?.Dispose();
    }
}