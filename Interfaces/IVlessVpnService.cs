using vtrace.Models;

namespace vtrace.Interfaces;

public interface IVlessVpnService
    {
        Task<bool> Connect(VlessConfig config);
        void Disconnect();
        bool IsConnected { get; }
        event EventHandler<string> ConnectionStatusChanged;
    }