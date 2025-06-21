using vtrace.Models;

namespace vtrace.ViewModels;

public class VlessConfigViewModel : ObservableObject
    {
        private readonly VlessConfig _config;
        private bool _isConnected;

        public VlessConfigViewModel(VlessConfig config)
        {
            _config = config;
        }

        public string Id => _config.Id;
        public string Address => _config.Address;
        public int Port => _config.Port;
        public string Remark => _config.Remark ?? $"{_config.Address}:{_config.Port}";
        
        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }
    }