using vtrace.Models;

namespace vtrace.Interfaces;

public interface IConfigStorageService
{
    Task AddConfigAsync(VlessConfig config);
    Task UpdateConfigAsync(VlessConfig config);
    Task RemoveConfigAsync(string configId);
    Task<VlessConfig> GetConfigAsync(string configId);
    Task<IEnumerable<VlessConfig>> GetAllConfigsAsync();
    Task<bool> ConfigExistsAsync(string configId);
}