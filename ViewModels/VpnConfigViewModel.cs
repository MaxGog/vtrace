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

    private readonly CircularBuffer<double> _downloadSpeeds = new(60);
    private readonly CircularBuffer<double> _uploadSpeeds = new(60);
    private DateTime _lastSpeedUpdateTime;
    private long _lastBytesReceived;
    private long _lastBytesSent;

    public ISeries[] SpeedSeries { get; }
    public Axis[] XAxes { get; }
    public Axis[] YAxes { get; }

    public bool IsVpnEnabled
    {
        get => _isVpnEnabled;
        set => SetProperty(ref _isVpnEnabled, value);
    }

    public bool UseTlsEncryption
    {
        get => _useTlsEncryption;
        set
        {
            if (SetProperty(ref _useTlsEncryption, value))
            {
                foreach (var config in Configs)
                {
                    config.Security = value ? "tls" : "none";
                }
            }
        }
    }

    public string ConnectionSpeed
    {
        get => _connectionSpeed;
        private set => SetProperty(ref _connectionSpeed, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsBusy));
            }
        }
    }

    public ObservableCollection<VlessConfigViewModel> Configs { get; } = new();

    public VlessConfigViewModel? ActiveConnection
    {
        get => _activeConnection;
        set => SetProperty(ref _activeConnection, value);
    }

    public string NewConfigUrl
    {
        get => _newConfigUrl;
        set => SetProperty(ref _newConfigUrl, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand AddConfigCommand { get; }
    public ICommand DeleteConfigCommand { get; }
    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }

    [Obsolete]
    public VpnConfigViewModel(IConfigStorageService configStorage, IVlessVpnService vpnService)
    {
        _configStorage = configStorage ?? throw new ArgumentNullException(nameof(configStorage));
        _vpnService = vpnService ?? throw new ArgumentNullException(nameof(vpnService));

        AddConfigCommand = new Command(async () => await AddConfig().ConfigureAwait(false));
        DeleteConfigCommand = new Command<string>(async (id) => await DeleteConfig(id).ConfigureAwait(false));
        ConnectCommand = new Command<string>(async (id) => await ConnectToVpn(id).ConfigureAwait(false));
        DisconnectCommand = new Command(DisconnectFromVpn);

        _vpnService.ConnectionStatusChanged += OnConnectionStatusChanged;

        SpeedSeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Name = "Download",
                Values = _downloadSpeeds,
                Stroke = new SolidColorPaint(SKColors.CornflowerBlue) { StrokeThickness = 2 },
                Fill = null,
                GeometrySize = 0
            },
            new LineSeries<double>
            {
                Name = "Upload",
                Values = _uploadSpeeds,
                Stroke = new SolidColorPaint(SKColors.OrangeRed) { StrokeThickness = 2 },
                Fill = null,
                GeometrySize = 0
            }
        };

        XAxes = new[] { CreateInvisibleAxis() };
        YAxes = new[] { CreateInvisibleAxis() };

        _ = LoadConfigsAsync();

        Device.StartTimer(TimeSpan.FromSeconds(1), UpdateSpeedIfConnected);
    }

    private static Axis CreateInvisibleAxis() => new()
    {
        IsVisible = false,
        LabelsPaint = new SolidColorPaint(SKColors.Transparent)
    };

    private bool UpdateSpeedIfConnected()
    {
        if (_vpnService.IsConnected)
        {
            UpdateSpeed();
        }
        return true;
    }

    private void UpdateSpeed()
    {
        try
        {
            var now = DateTime.Now;
            var timeElapsed = (now - _lastSpeedUpdateTime).TotalSeconds;
            
            if (timeElapsed <= 0) return;

            var bytesReceived = _vpnService.BytesReceived;
            var bytesSent = _vpnService.BytesSent;

            var downloadSpeed = (bytesReceived - _lastBytesReceived) / timeElapsed;
            var uploadSpeed = (bytesSent - _lastBytesSent) / timeElapsed;

            _lastBytesReceived = bytesReceived;
            _lastBytesSent = bytesSent;
            _lastSpeedUpdateTime = now;

            ConnectionSpeed = $"{FormatSpeed(downloadSpeed)} ↓ | {FormatSpeed(uploadSpeed)} ↑";

            _downloadSpeeds.Add(downloadSpeed / 1024);
            _uploadSpeeds.Add(uploadSpeed / 1024);

            OnPropertyChanged(nameof(SpeedSeries));
        }
        catch
        {
            // Логирование ошибок можно добавить здесь
        }
    }

    private static string FormatSpeed(double bytesPerSecond)
    {
        return bytesPerSecond >= 1024 * 1024
            ? $"{(bytesPerSecond / (1024 * 1024)):0.0} MB/s"
            : $"{(bytesPerSecond / 1024):0.0} KB/s";
    }

    private async Task LoadConfigsAsync()
    {
        try
        {
            IsBusy = true;
            Configs.Clear();

            var configs = await _configStorage.GetAllConfigsAsync().ConfigureAwait(false);
            foreach (var config in configs)
            {
                Configs.Add(new VlessConfigViewModel(config));
            }
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
            
            ResetAllConnectionStatuses();
            
            var success = await _vpnService.Connect(config).ConfigureAwait(false);
            if (success)
            {
                var connectedVm = Configs.FirstOrDefault(c => c.Id == id);
                if (connectedVm != null)
                {
                    connectedVm.IsConnected = true;
                    connectedVm.LastError = null;
                    ActiveConnection = connectedVm;
                    StatusMessage = "Connected successfully";
                    
                    _downloadSpeeds.Clear();
                    _uploadSpeeds.Clear();
                    _lastBytesReceived = 0;
                    _lastBytesSent = 0;
                    _lastSpeedUpdateTime = DateTime.Now;
                }
            }
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
        if (failedVm != null)
        {
            failedVm.LastError = errorMessage;
        }
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

    private void OnConnectionStatusChanged(object sender, string status)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (status != "Disconnected" || string.IsNullOrEmpty(StatusMessage))
            {
                StatusMessage = status;
            }
        });
    }
}

public class CircularBuffer<T> : List<T>
{
    private readonly int _capacity;

    public CircularBuffer(int capacity)
    {
        _capacity = capacity;
    }

    public new void Add(T item)
    {
        if (Count >= _capacity)
        {
            RemoveAt(0);
        }
        base.Add(item);
    }
}