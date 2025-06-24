using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using vtrace.Models;

namespace vtrace.Services;

internal class NetworkConnectionManager : IDisposable
{
    private const int CONNECTION_TIMEOUT_MS = 10000;
    private const int KEEPALIVE_INTERVAL_MS = 30000;
    
    private TcpClient? _tcpClient;
    private Stream? _networkStream;
    private CancellationTokenSource? _cts;
    
    private long _bytesReceived;
    private long _bytesSent;
    private volatile bool _isConnected;
    private DateTime _lastActivityTime;
    
    private Task? _connectionMonitorTask;
    private Task? _keepAliveTask;
    
    public long BytesReceived => _bytesReceived;
    public long BytesSent => _bytesSent;
    public bool IsConnected => _isConnected;
    public TimeSpan Uptime => _isConnected ? DateTime.UtcNow - _lastActivityTime : TimeSpan.Zero;
    
    public event EventHandler<string>? ConnectionStatusChanged;
    public event EventHandler<long>? DataTransferred;

    public async Task EstablishConnection(VlessConfig config)
    {
        if (_isConnected) Disconnect();
        
        _cts = new CancellationTokenSource();
        _lastActivityTime = DateTime.UtcNow;

        try
        {
            NotifyStatus("Connecting...");
            await EstablishTcpConnection(config);
            await EstablishSecurityLayer(config);
            StartMonitoringTasks();
            _isConnected = true;
            NotifyStatus("Connected successfully");
        }
        catch (Exception ex)
        {
            NotifyStatus($"Connection failed: {ex.Message}");
            Disconnect();
            throw;
        }
    }

    private async Task EstablishTcpConnection(VlessConfig config)
    {
        _tcpClient = new TcpClient {
            SendTimeout = CONNECTION_TIMEOUT_MS,
            ReceiveTimeout = CONNECTION_TIMEOUT_MS
        };
        
        _tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        
        NotifyStatus($"Connecting to {config.Address}:{config.Port}...");
        
        var connectTask = _tcpClient.ConnectAsync(config.Address, config.Port);
        var timeoutTask = Task.Delay(CONNECTION_TIMEOUT_MS, _cts!.Token);
        
        var completedTask = await Task.WhenAny(connectTask, timeoutTask);
        if (completedTask == timeoutTask)
            throw new TimeoutException("TCP connection timeout");
        
        await connectTask;
        _networkStream = _tcpClient.GetStream();
        _lastActivityTime = DateTime.UtcNow;
        NotifyStatus("TCP connection established");
    }

    private async Task EstablishSecurityLayer(VlessConfig config)
    {
        if (config.Security == "none")
        {
            NotifyStatus("Skipping TLS (insecure mode)");
            return;
        }

        if (config.Security == "tls" || config.Security == "reality" || config.Port == 443)
        {
            NotifyStatus("Establishing TLS layer...");
            
            var sslOptions = new SslClientAuthenticationOptions
            {
                TargetHost = config.Sni ?? config.Address,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                EncryptionPolicy = EncryptionPolicy.RequireEncryption,
                RemoteCertificateValidationCallback = (sender, cert, chain, errors) => 
                    CertificateValidator.Validate(cert, errors, config)
            };

            var sslStream = new SslStream(_networkStream!, false);
            await sslStream.AuthenticateAsClientAsync(sslOptions, _cts!.Token);
            
            _networkStream = sslStream;
            _lastActivityTime = DateTime.UtcNow;
            NotifyStatus("TLS handshake completed");
        }
    }

    public async Task<int> SendAsync(byte[] buffer, int offset, int count)
    {
        if (!_isConnected || _networkStream == null)
            throw new InvalidOperationException("Not connected");
        
        await _networkStream.WriteAsync(buffer, offset, count, _cts!.Token);
        await _networkStream.FlushAsync(_cts.Token);
        
        Interlocked.Add(ref _bytesSent, count);
        _lastActivityTime = DateTime.UtcNow;
        DataTransferred?.Invoke(this, count);
        
        return count;
    }

    public async Task<int> ReceiveAsync(byte[] buffer, int offset, int count)
    {
        if (!_isConnected || _networkStream == null)
            throw new InvalidOperationException("Not connected");
        
        var bytesRead = await _networkStream.ReadAsync(buffer, offset, count, _cts!.Token);
        
        if (bytesRead > 0)
        {
            Interlocked.Add(ref _bytesReceived, bytesRead);
            _lastActivityTime = DateTime.UtcNow;
            DataTransferred?.Invoke(this, bytesRead);
        }
        else
        {
            Disconnect();
        }
        
        return bytesRead;
    }

    public void Disconnect()
    {
        try
        {
            _cts?.Cancel();
            _networkStream?.Dispose();
            _tcpClient?.Dispose();
            _isConnected = false;
            NotifyStatus("Disconnected");
        }
        finally
        {
            _networkStream = null;
            _tcpClient = null;
        }
    }

    private void StartMonitoringTasks()
    {
        _connectionMonitorTask = Task.Run(MonitorConnection, _cts!.Token);
        _keepAliveTask = Task.Run(SendKeepAlives, _cts.Token);
    }

    private async Task MonitorConnection()
    {
        var buffer = new byte[1];
        while (!_cts!.IsCancellationRequested)
        {
            try
            {
                if (await _networkStream!.ReadAsync(buffer, 0, 0, _cts.Token) == 0)
                    break;
                await Task.Delay(1000, _cts.Token);
            }
            catch { break; }
        }
        Disconnect();
    }

    private async Task SendKeepAlives()
    {
        while (!_cts!.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(KEEPALIVE_INTERVAL_MS, _cts.Token);
                if (_isConnected)
                {
                    var pingPacket = new byte[] { 0x00 };
                    await SendAsync(pingPacket, 0, pingPacket.Length);
                }
            }
            catch { break; }
        }
    }

    private void NotifyStatus(string status) 
        => ConnectionStatusChanged?.Invoke(this, status);

    public void Dispose() => Disconnect();
}