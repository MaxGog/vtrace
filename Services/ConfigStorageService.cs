using vtrace.Interfaces;
using vtrace.Models;

namespace vtrace.Services;

public class InMemoryConfigStorageService : IConfigStorageService
{
    private readonly Dictionary<string, VlessConfig> _configs = new Dictionary<string, VlessConfig>();
    private readonly object _lock = new object();

    public Task AddConfigAsync(VlessConfig config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        
        if (string.IsNullOrWhiteSpace(config.Id))
            throw new ArgumentException("Config ID cannot be empty");

        lock (_lock)
        {
            if (_configs.ContainsKey(config.Id))
                throw new InvalidOperationException($"Config with ID {config.Id} already exists");

            _configs[config.Id] = config;
        }

        return Task.CompletedTask;
    }

    public Task UpdateConfigAsync(VlessConfig config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        
        if (string.IsNullOrWhiteSpace(config.Id))
            throw new ArgumentException("Config ID cannot be empty");

        lock (_lock)
        {
            if (!_configs.ContainsKey(config.Id))
                throw new KeyNotFoundException($"Config with ID {config.Id} not found");

            _configs[config.Id] = config;
        }

        return Task.CompletedTask;
    }

    public Task RemoveConfigAsync(string configId)
    {
        if (string.IsNullOrWhiteSpace(configId))
            throw new ArgumentException("Config ID cannot be empty");

        lock (_lock)
        {
            if (!_configs.ContainsKey(configId))
                throw new KeyNotFoundException($"Config with ID {configId} not found");

            _configs.Remove(configId);
        }

        return Task.CompletedTask;
    }

    public Task<VlessConfig> GetConfigAsync(string configId)
    {
        if (string.IsNullOrWhiteSpace(configId))
            throw new ArgumentException("Config ID cannot be empty");

        lock (_lock)
        {
            if (!_configs.TryGetValue(configId, out var config))
                throw new KeyNotFoundException($"Config with ID {configId} not found");

            return Task.FromResult(config);
        }
    }

    public Task<IEnumerable<VlessConfig>> GetAllConfigsAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_configs.Values.AsEnumerable());
        }
    }

    public Task<bool> ConfigExistsAsync(string configId)
    {
        if (string.IsNullOrWhiteSpace(configId))
            throw new ArgumentException("Config ID cannot be empty");

        lock (_lock)
        {
            return Task.FromResult(_configs.ContainsKey(configId));
        }
    }
}