using System.Collections.Concurrent;
using AIWorkspace.API.Models;

namespace AIWorkspace.API.Repositories;

public class InMemoryRepository<T> : IRepository<T> where T : UserDocument
{
    private readonly ConcurrentDictionary<string, T> _items = new();

    // Newest first; when a limit is given we return the newest N (oldest->newest order is restored by the caller if needed).
    public Task<List<T>> ListAsync(string userId, int? limit = null)
    {
        var q = _items.Values.Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt);
        var list = limit.HasValue ? q.Take(limit.Value).ToList() : q.ToList();
        return Task.FromResult(list);
    }

    public Task<T?> GetAsync(string userId, string id) =>
        Task.FromResult(_items.TryGetValue(id, out var v) && v.UserId == userId ? v : null);

    public Task AddAsync(T item) { _items[item.Id] = item; return Task.CompletedTask; }

    public Task UpdateAsync(T item) { _items[item.Id] = item; return Task.CompletedTask; }

    public Task<bool> DeleteAsync(string userId, string id)
    {
        if (_items.TryGetValue(id, out var v) && v.UserId == userId)
            return Task.FromResult(_items.TryRemove(id, out _));
        return Task.FromResult(false);
    }

    public Task DeleteAllAsync(string userId)
    {
        foreach (var kv in _items.Where(k => k.Value.UserId == userId).ToList())
            _items.TryRemove(kv.Key, out _);
        return Task.CompletedTask;
    }
}
