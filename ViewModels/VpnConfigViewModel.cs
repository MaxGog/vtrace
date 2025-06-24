using System.Collections.ObjectModel;
using System.Windows.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using vtrace.Interfaces;
using vtrace.Models;

namespace vtrace.ViewModels;

public class VpnConfigViewModel : ObservableObject
{
    private readonly IConfigStorageService _configStorage;
    private readonly IVlessVpnService _vpnService;
    
    private VlessConfigViewModel? _activeConnection;
    private string _newConfigUrl = string.Empty;
    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private string _connectionSpeed = "0 KB/s ↓ | 0 KB/s ↑";
    private bool _isVpnEnabled = true;
    private bool _useTlsEncryption = true;
    private bool _isMonitoringVisible = true;
    private bool _isSettingsVisible = true;
    private string _selectedSecurityType = "tls";
    private string _selectedFingerprint = "chrome";
    private string _sni = "yahoo.com";
    private string _publicKey = string.Empty;
    private string _shortId = string.Empty;
    private string _spiderX = string.Empty;
    private string _selectedFlowType = "xtls-rprx-vision";

    private readonly CircularBuffer<double> _downloadSpeeds = new(60);
    private readonly CircularBuffer<double> _uploadSpeeds = new(60);
    private DateTime _lastSpeedUpdateTime;
    private long _lastBytesReceived;
    private long _lastBytesSent;

    public ObservableCollection<VlessConfigViewModel> Configs { get; } = new();
    public ObservableCollection<string> SecurityTypes { get; } = new() { "none", "tls", "reality" };
    public ObservableCollection<string> Fingerprints { get; } = new() { "chrome", "firefox", "safari", "random" };
    public ObservableCollection<string> FlowTypes { get; } = new() { "xtls-rprx-vision", "xtls-rprx-direct" };

    public ISeries[] SpeedSeries { get; }
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }

    public ICommand AddConfigCommand { get; }
    public ICommand DeleteConfigCommand { get; }
    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }
    public ICommand QuickRealityConnectCommand { get; }
    public ICommand ToggleMonitoringCommand { get; }
    public ICommand ToggleSettingsCommand { get; }

    public bool IsVpnEnabled { get => _isVpnEnabled; set => SetProperty(ref _isVpnEnabled, value); }
    public bool UseTlsEncryption { get => _useTlsEncryption; set => SetProperty(ref _useTlsEncryption, value); }
    public string ConnectionSpeed { get => _connectionSpeed; private set => SetProperty(ref _connectionSpeed, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }
    public VlessConfigViewModel? ActiveConnection { get => _activeConnection; set => SetProperty(ref _activeConnection, value); }
    public string NewConfigUrl { get => _newConfigUrl; set => SetProperty(ref _newConfigUrl, value); }
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public bool IsMonitoringVisible { get => _isMonitoringVisible; set => SetProperty(ref _isMonitoringVisible, value); }
    public bool IsSettingsVisible { get => _isSettingsVisible; set => SetProperty(ref _isSettingsVisible, value); }
    public string SelectedSecurityType { get => _selectedSecurityType; set => SetProperty(ref _selectedSecurityType, value); }
    public string SelectedFingerprint { get => _selectedFingerprint; set => SetProperty(ref _selectedFingerprint, value); }
    public string Sni { get => _sni; set => SetProperty(ref _sni, value); }
    public string PublicKey { get => _publicKey; set => SetProperty(ref _publicKey, value); }
    public string ShortId { get => _shortId; set => SetProperty(ref _shortId, value); }
    public string SpiderX { get => _spiderX; set => SetProperty(ref _spiderX, value); }
    public string SelectedFlowType { get => _selectedFlowType; set => SetProperty(ref _selectedFlowType, value); }

    public VpnConfigViewModel(IConfigStorageService configStorage, IVlessVpnService vpnService)
    {
        _configStorage = configStorage ?? throw new ArgumentNullException(nameof(configStorage));
        _vpnService = vpnService ?? throw new ArgumentNullException(nameof(vpnService));

        InitializeCommands();
        InitializeChart();
        _vpnService.ConnectionStatusChanged += OnConnectionStatusChanged;

        _ = LoadConfigsAsync();
        Device.StartTimer(TimeSpan.FromSeconds(1), UpdateSpeedIfConnected);
    }

    public VpnConfigViewModel() : this(null!, null!)
    {
        QuickRealityConnectCommand = new Command(async () => await QuickRealityConnect());
        ToggleMonitoringCommand = new Command(() => IsMonitoringVisible = !IsMonitoringVisible);
        ToggleSettingsCommand = new Command(() => IsSettingsVisible = !IsSettingsVisible);
    }

    private void InitializeCommands()
    {
        AddConfigCommand = new Command(async () => await AddConfig().ConfigureAwait(false));
        DeleteConfigCommand = new Command<string>(async (id) => await DeleteConfig(id).ConfigureAwait(false));
        ConnectCommand = new Command<string>(async (id) => await ConnectToVpn(id).ConfigureAwait(false));
        DisconnectCommand = new Command(DisconnectFromVpn);
    }

    private void InitializeChart()
    {
        SpeedSeries = new ISeries[]
        {
            CreateSeries("Download", _downloadSpeeds, SKColors.CornflowerBlue),
            CreateSeries("Upload", _uploadSpeeds, SKColors.OrangeRed)
        };

        XAxes = new[] { CreateInvisibleAxis() };
        YAxes = new[] { CreateInvisibleAxis() };
    }

    private static ISeries CreateSeries(string name, IEnumerable<double> values, SKColor color) => new LineSeries<double>
    {
        Name = name,
        Values = values,
        Stroke = new SolidColorPaint(color) { StrokeThickness = 2 },
        Fill = null,
        GeometrySize = 0
    };

    private static Axis CreateInvisibleAxis() => new()
    {
        IsVisible = false,
        LabelsPaint = new SolidColorPaint(SKColors.Transparent)
    };

    private async Task QuickRealityConnect()
    {
        try
        {
            IsBusy = true;
            var config = new VlessConfig
            {
                Address = "80.85.241.40",
                Port = 45981,
                Id = "94b6d4ba-a2c2-42f0-aa06-754990a99c3b",
                Security = SelectedSecurityType,
                PublicKey = PublicKey,
                Fingerprint = SelectedFingerprint,
                Sni = Sni,
                ShortId = ShortId,
                SpiderX = SpiderX,
                Flow = SelectedFlowType,
                Remark = "Reality Connection"
            };
            
            await ConnectWithConfig(config, "Reality connection established");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool UpdateSpeedIfConnected()
    {
        if (_vpnService.IsConnected) UpdateSpeed();
        return true;
    }

    private void UpdateSpeed()
    {
        try
        {
            var now = DateTime.Now;
            var timeElapsed = (now - _lastSpeedUpdateTime).TotalSeconds;
            if (timeElapsed <= 0) return;

            var (bytesReceived, bytesSent) = (_vpnService.BytesReceived, _vpnService.BytesSent);
            var (downloadSpeed, uploadSpeed) = CalculateSpeeds(bytesReceived, bytesSent, timeElapsed);

            UpdateConnectionStats(bytesReceived, bytesSent, now, downloadSpeed, uploadSpeed);
        }
        catch { /* Log errors here */ }
    }

    private (double download, double upload) CalculateSpeeds(long bytesReceived, long bytesSent, double timeElapsed) => 
        ((bytesReceived - _lastBytesReceived) / timeElapsed, 
         (bytesSent - _lastBytesSent) / timeElapsed);

    private void UpdateConnectionStats(long bytesReceived, long bytesSent, DateTime now, double downloadSpeed, double uploadSpeed)
    {
        _lastBytesReceived = bytesReceived;
        _lastBytesSent = bytesSent;
        _lastSpeedUpdateTime = now;

        ConnectionSpeed = $"{FormatSpeed(downloadSpeed)} ↓ | {FormatSpeed(uploadSpeed)} ↑";

        _downloadSpeeds.Add(downloadSpeed / 1024);
        _uploadSpeeds.Add(uploadSpeed / 1024);

        OnPropertyChanged(nameof(SpeedSeries));
    }

    private static string FormatSpeed(double bytesPerSecond) => 
        bytesPerSecond >= 1024 * 1024
            ? $"{(bytesPerSecond / (1024 * 1024)):0.0} MB/s"
            : $"{(bytesPerSecond / 1024):0.0} KB/s";

    private async Task LoadConfigsAsync()
    {
        try
        {
            IsBusy = true;
            Configs.Clear();

            var configs = await _configStorage.GetAllConfigsAsync().ConfigureAwait(false);
            foreach (var config in configs) Configs.Add(new VlessConfigViewModel(config));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load configs: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AddConfig()
    {
        if (string.IsNullOrWhiteSpace(NewConfigUrl))
        {
            StatusMessage = "Please enter a valid VLESS URL";
            return;
        }

        try
        {
            IsBusy = true;
            var config = VlessConfig.Parse(NewConfigUrl);
            await _configStorage.AddConfigAsync(config).ConfigureAwait(false);
            Configs.Add(new VlessConfigViewModel(config));
            NewConfigUrl = string.Empty;
            StatusMessage = "Configuration added successfully";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to add config: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteConfig(string id)
    {
        if (string.IsNullOrEmpty(id)) return;

        try
        {
            IsBusy = true;
            await _configStorage.RemoveConfigAsync(id).ConfigureAwait(false);
            
            var item = Configs.FirstOrDefault(c => c.Id == id);
            if (item != null)
            {
                Configs.Remove(item);
                StatusMessage = "Configuration removed successfully";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to remove config: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ConnectToVpn(string id)
    {
        if (string.IsNullOrEmpty(id)) return;

        try
        {
            IsBusy = true;
            var config = await _configStorage.GetConfigAsync(id).ConfigureAwait(false);
            config.Security = UseTlsEncryption ? "tls" : "none";
            
            await ConnectWithConfig(config, "Connected successfully");
        }
        catch (Exception ex)
        {
            UpdateFailedConnectionStatus(id, ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ConnectWithConfig(VlessConfig config, string successMessage)
    {
        ResetAllConnectionStatuses();
        var success = await _vpnService.Connect(config).ConfigureAwait(false);
        
        if (!success) return;
        
        var connectedVm = Configs.FirstOrDefault(c => c.Id == config.Id);
        if (connectedVm != null)
        {
            connectedVm.IsConnected = true;
            connectedVm.LastError = null;
            ActiveConnection = connectedVm;
            StatusMessage = successMessage;
            
            ResetSpeedStats();
        }
    }

    private void ResetSpeedStats()
    {
        _downloadSpeeds.Clear();
        _uploadSpeeds.Clear();
        _lastBytesReceived = 0;
        _lastBytesSent = 0;
        _lastSpeedUpdateTime = DateTime.Now;
    }

    private void ResetAllConnectionStatuses()
    {
        foreach (var vm in Configs)
        {
            vm.IsConnected = false;
            vm.LastError = null;
        }
    }

    private void UpdateFailedConnectionStatus(string id, string errorMessage)
    {
        var failedVm = Configs.FirstOrDefault(c => c.Id == id);
        if (failedVm != null) failedVm.LastError = errorMessage;
    }

    private void DisconnectFromVpn()
    {
        try
        {
            _vpnService.Disconnect();
            ResetAllConnectionStatuses();
            ActiveConnection = null;
            StatusMessage = "Disconnected";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Disconnection failed: {ex.Message}";
        }
    }

    private void OnConnectionStatusChanged(object sender, string status) => 
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (status != "Disconnected" || string.IsNullOrEmpty(StatusMessage))
                StatusMessage = status;
        });
}

public class CircularBuffer<T> : List<T>
{
    private readonly int _capacity;

    public CircularBuffer(int capacity) => _capacity = capacity;

    public new void Add(T item)
    {
        if (Count >= _capacity) RemoveAt(0);
        base.Add(item);
    }
}