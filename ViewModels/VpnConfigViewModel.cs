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
    private string _newConfigUrl;
    private bool _isBusy;
    private string _statusMessage;

    private string _connectionSpeed = "0 KB/s ↓ | 0 KB/s ↑";
    private readonly List<double> _downloadSpeeds = new();
    private readonly List<double> _uploadSpeeds = new();
    private DateTime _lastSpeedUpdateTime;
    private long _lastBytesReceived;
    private long _lastBytesSent;

    public ISeries[] SpeedSeries { get; set; }
    public Axis[] XAxes { get; set; }
    public Axis[] YAxes { get; set; }

    private bool _isVpnEnabled = true;
    public bool IsVpnEnabled
    {
        get => _isVpnEnabled;
        set => SetProperty(ref _isVpnEnabled, value);
    }


    public string ConnectionSpeed
    {
        get => _connectionSpeed;
        set => SetProperty(ref _connectionSpeed, value);
    }

    public ObservableCollection<VlessConfigViewModel> Configs { get; } = new();

    public string NewConfigUrl
    {
        get => _newConfigUrl;
        set => SetProperty(ref _newConfigUrl, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public ICommand AddConfigCommand { get; }
    public ICommand DeleteConfigCommand { get; }
    public ICommand ConnectCommand { get; }
    public ICommand DisconnectCommand { get; }

    [Obsolete]
    public VpnConfigViewModel(IConfigStorageService configStorage, IVlessVpnService vpnService)
    {
        _configStorage = configStorage;
        _vpnService = vpnService;

        AddConfigCommand = new Command(async () => await AddConfig());
        DeleteConfigCommand = new Command<string>(async (id) => await DeleteConfig(id));
        ConnectCommand = new Command<string>(async (id) => await ConnectToVpn(id));
        DisconnectCommand = new Command(() => DisconnectFromVpn());

        _vpnService.ConnectionStatusChanged += OnConnectionStatusChanged;

        LoadConfigs();

        _configStorage = configStorage;
        _vpnService = vpnService;

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

        XAxes = new[]
        {
            new Axis
            {
                IsVisible = false,
                LabelsPaint = new SolidColorPaint(SKColors.Transparent)
            }
        };

        YAxes = new[]
        {
            new Axis
            {
                IsVisible = false,
                LabelsPaint = new SolidColorPaint(SKColors.Transparent)
            }
        };
        
        Device.StartTimer(TimeSpan.FromSeconds(1), () =>
        {
            if (_vpnService.IsConnected)
            {
                UpdateSpeed();
            }
            return true;
        });
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

            if (_downloadSpeeds.Count > 60)
            {
                _downloadSpeeds.RemoveAt(0);
                _uploadSpeeds.RemoveAt(0);
            }

            OnPropertyChanged(nameof(SpeedSeries));
        }
        catch
        {
            // Игнорируем ошибки
        }
    }

    private string FormatSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond >= 1024 * 1024)
        {
            return $"{(bytesPerSecond / (1024 * 1024)):0.0} MB/s";
        }
        return $"{(bytesPerSecond / 1024):0.0} KB/s";
    }

    private async void LoadConfigs()
    {
        try
        {
            IsBusy = true;
            Configs.Clear();

            var configs = await _configStorage.GetAllConfigsAsync();
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
            await _configStorage.AddConfigAsync(config);
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
        try
        {
            IsBusy = true;
            await _configStorage.RemoveConfigAsync(id);
            var item = Configs.FirstOrDefault(c => c.Id == id);
            if (item != null)
            {
                Configs.Remove(item);
            }
            StatusMessage = "Configuration removed successfully";
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
        try
        {
            IsBusy = true;
            var config = await _configStorage.GetConfigAsync(id);
            
            foreach (var vm in Configs)
            {
                vm.IsConnected = false;
                vm.LastError = null;
            }
            
            var success = await _vpnService.Connect(config);
            if (success)
            {
                var connectedVm = Configs.FirstOrDefault(c => c.Id == id);
                if (connectedVm != null)
                {
                    connectedVm.IsConnected = true;
                    connectedVm.LastError = null;
                    StatusMessage = "Connected successfully";
                }
            }
        }
        catch (Exception ex)
        {
            var failedVm = Configs.FirstOrDefault(c => c.Id == id);
            if (failedVm != null)
            {
                failedVm.LastError = ex.Message;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void DisconnectFromVpn()
    {
        try
        {
            _vpnService.Disconnect();
            foreach (var vm in Configs)
            {
                vm.IsConnected = false;
            }
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