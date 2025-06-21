using vtrace.Interfaces;
using vtrace.Models;

namespace vtrace.Controllers;

public class ConfigController
{
    private readonly IConfigStorageService _configStorage;

    public ConfigController(IConfigStorageService configStorage)
    {
        _configStorage = configStorage;
    }

    public async Task AddConfig(string vlessUrl)
    {
        var config = VlessConfig.Parse(vlessUrl);
        await _configStorage.AddConfigAsync(config);
    }

    public async Task<List<VlessConfig>> GetAllConfigs()
    {
        return (await _configStorage.GetAllConfigsAsync()).ToList();
    }

    public async Task ConnectToConfig(string configId, IVlessVpnService vpnService)
    {
        var config = await _configStorage.GetConfigAsync(configId);
        await vpnService.Connect(config);
    }
}