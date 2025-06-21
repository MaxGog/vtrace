using System.Text.Json;
using vtrace.Interfaces;
using vtrace.Models;

namespace vtrace.Services;

public sealed class JsonFileConfigStorageService : IConfigStorageService, IDisposable
{
    private const int FileBufferSize = 4096;
    private readonly string _storageFilePath;
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, VlessConfig> _configs;
    private bool _disposed;

    public JsonFileConfigStorageService(string storageFilePath)
    {
        _storageFilePath = storageFilePath ?? throw new ArgumentNullException(nameof(storageFilePath));
        _configs = LoadConfigsFromFile();
    }

    private Dictionary<string, VlessConfig> LoadConfigsFromFile()
    {
        lock (_syncRoot)
        {
            if (!File.Exists(_storageFilePath))
            {
                return new Dictionary<string, VlessConfig>();
            }

            try
            {
                using var fileStream = new FileStream(
                    _storageFilePath, 
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    FileBufferSize,
                    FileOptions.SequentialScan);

                var configs = JsonSerializer.Deserialize<List<VlessConfig>>(fileStream) 
                    ?? new List<VlessConfig>();

                return configs.ToDictionary(c => c.Id);
            }
            catch (JsonException)
            {
                // Log error here if needed
                return new Dictionary<string, VlessConfig>();
            }
        }
    }

    private async Task SaveConfigsToFileAsync()
    {
        byte[] jsonData;
        
        lock (_syncRoot)
        {
            jsonData = JsonSerializer.SerializeToUtf8Bytes(_configs.Values);
        }

        try
        {
            await using var fileStream = new FileStream(
                _storageFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                FileBufferSize,
                FileOptions.Asynchronous | FileOptions.WriteThrough);

            await fileStream.WriteAsync(jsonData).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Log error here
            throw new InvalidOperationException("Failed to save configurations", ex);
        }
    }

    public async Task AddConfigAsync(VlessConfig config)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(JsonFileConfigStorageService));
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (string.IsNullOrWhiteSpace(config.Id)) throw new ArgumentException("Config ID cannot be empty");

        lock (_syncRoot)
        {
            if (_configs.ContainsKey(config.Id))
            {
                throw new InvalidOperationException($"Config with ID {config.Id} already exists");
            }

            _configs[config.Id] = config;
        }

        await SaveConfigsToFileAsync().ConfigureAwait(false);
    }

    public async Task UpdateConfigAsync(VlessConfig config)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(JsonFileConfigStorageService));
        if (config == null) throw new ArgumentNullException(nameof(config));
        if (string.IsNullOrWhiteSpace(config.Id)) throw new ArgumentException("Config ID cannot be empty");

        lock (_syncRoot)
        {
            if (!_configs.ContainsKey(config.Id))
            {
                throw new KeyNotFoundException($"Config with ID {config.Id} not found");
            }

            _configs[config.Id] = config;
        }

        await SaveConfigsToFileAsync().ConfigureAwait(false);
    }

    public async Task RemoveConfigAsync(string configId)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(JsonFileConfigStorageService));
        if (string.IsNullOrWhiteSpace(configId)) throw new ArgumentException("Config ID cannot be empty");

        lock (_syncRoot)
        {
            if (!_configs.Remove(configId))
            {
                throw new KeyNotFoundException($"Config with ID {configId} not found");
            }
        }

        await SaveConfigsToFileAsync().ConfigureAwait(false);
    }

    public Task<VlessConfig> GetConfigAsync(string configId)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(JsonFileConfigStorageService));
        if (string.IsNullOrWhiteSpace(configId)) throw new ArgumentException("Config ID cannot be empty");

        lock (_syncRoot)
        {
            return _configs.TryGetValue(configId, out var config) 
                ? Task.FromResult(config) 
                : throw new KeyNotFoundException($"Config with ID {configId} not found");
        }
    }

    public Task<IEnumerable<VlessConfig>> GetAllConfigsAsync()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(JsonFileConfigStorageService));

        lock (_syncRoot)
        {
            return Task.FromResult(_configs.Values.ToList().AsEnumerable());
        }
    }

    public Task<bool> ConfigExistsAsync(string configId)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(JsonFileConfigStorageService));
        if (string.IsNullOrWhiteSpace(configId)) throw new ArgumentException("Config ID cannot be empty");

        lock (_syncRoot)
        {
            return Task.FromResult(_configs.ContainsKey(configId));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        lock (_syncRoot)
        {
            _configs.Clear();
            _disposed = true;
        }
    }
}