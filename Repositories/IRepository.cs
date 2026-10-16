using AIWorkspace.API.Models;

namespace AIWorkspace.API.Repositories;

/// <summary>Storage abstraction (Dependency Inversion): controllers never know if data is in memory or in MongoDB.</summary>
public interface IRepository<T> where T : UserDocument
{
    Task<List<T>> ListAsync(string userId, int? limit = null);
    Task<T?> GetAsync(string userId, string id);
    Task AddAsync(T item);
    Task UpdateAsync(T item);
    Task<bool> DeleteAsync(string userId, string id);
    Task DeleteAllAsync(string userId);
}
