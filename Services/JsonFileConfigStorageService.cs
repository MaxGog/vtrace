using System.Text.Json;
using vtrace.Interfaces;
using vtrace.Models;

namespace vtrace.Services;

public class JsonFileConfigStorageService : IConfigStorageService
{
    private readonly string _storageFilePath;
    private readonly object _lock = new object();
    private Dictionary<string, VlessConfig> _configs;

    public JsonFileConfigStorageService(string storageFilePath)
    {
        _storageFilePath = storageFilePath ?? throw new ArgumentNullException(nameof(storageFilePath));
        _configs = LoadConfigsFromFile();
    }

    private Dictionary<string, VlessConfig> LoadConfigsFromFile()
    {
        lock (_lock)
        {
            if (!File.Exists(_storageFilePath))
                return new Dictionary<string, VlessConfig>();

            var json = File.ReadAllText(_storageFilePath);
            var configs = JsonSerializer.Deserialize<List<VlessConfig>>(json) 
                ?? new List<VlessConfig>();
            
            return configs.ToDictionary(c => c.Id, c => c);
        }
    }

    private async Task SaveConfigsToFileAsync()
    {
        lock (_lock)
        {
            var json = JsonSerializer.Serialize(_configs.Values.ToList());
            File.WriteAllText(_storageFilePath, json);
        }
    }

    public async Task AddConfigAsync(VlessConfig config)
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

        await SaveConfigsToFileAsync();
    }

    public async Task UpdateConfigAsync(VlessConfig config)
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

        await SaveConfigsToFileAsync();
    }

    public async Task RemoveConfigAsync(string configId)
    {
        if (string.IsNullOrWhiteSpace(configId))
            throw new ArgumentException("Config ID cannot be empty");

        lock (_lock)
        {
            if (!_configs.ContainsKey(configId))
                throw new KeyNotFoundException($"Config with ID {configId} not found");

            _configs.Remove(configId);
        }

        await SaveConfigsToFileAsync();
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