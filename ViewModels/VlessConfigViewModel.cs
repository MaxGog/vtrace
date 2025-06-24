using vtrace.Models;

namespace vtrace.ViewModels;

public class VlessConfigViewModel(VlessConfig config) : ObservableObject
{
    private readonly VlessConfig _config = config;
    private bool _isConnected;
    private string? _lastError;

    public string Id => _config.Id;
    public string Address => _config.Address;
    public int Port => _config.Port;
    public string Remark => _config.Remark ?? $"{_config.Address}:{_config.Port}";

    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    public string LastError
    {
        get => _lastError;
        set => SetProperty(ref _lastError, value);
    }
    
    private string _security = config.Security;

    public string Security
    {
        get => _security;
        set => SetProperty(ref _security, value);
    }
}