using AIWorkspace.API.Models;
using MongoDB.Driver;

namespace AIWorkspace.API.Repositories;

public class MongoRepository<T> : IRepository<T> where T : UserDocument
{
    private readonly IMongoCollection<T> _col;

    public MongoRepository(IMongoDatabase db, string collectionName) =>
        _col = db.GetCollection<T>(collectionName);

    private static FilterDefinition<T> ByUser(string userId) =>
        Builders<T>.Filter.Eq("UserId", userId);

    private static FilterDefinition<T> ByUserAndId(string userId, string id) =>
        Builders<T>.Filter.And(Builders<T>.Filter.Eq("UserId", userId), Builders<T>.Filter.Eq("_id", id));

    public async Task<List<T>> ListAsync(string userId, int? limit = null) =>
        await _col.Find(ByUser(userId))
                  .Sort(Builders<T>.Sort.Descending("CreatedAt"))
                  .Limit(limit)
                  .ToListAsync();

    public async Task<T?> GetAsync(string userId, string id) =>
        await _col.Find(ByUserAndId(userId, id)).FirstOrDefaultAsync();

    public Task AddAsync(T item) => _col.InsertOneAsync(item);

    public Task UpdateAsync(T item) => _col.ReplaceOneAsync(ByUserAndId(item.UserId, item.Id), item);

    public async Task<bool> DeleteAsync(string userId, string id)
    {
        var r = await _col.DeleteOneAsync(ByUserAndId(userId, id));
        return r.DeletedCount > 0;
    }

    public Task DeleteAllAsync(string userId) => _col.DeleteManyAsync(ByUser(userId));
}
