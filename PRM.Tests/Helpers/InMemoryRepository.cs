using System.Linq.Expressions;
using PRM.Application.Interfaces.Repository;
using PRM.Domain.Entities;

namespace PRM.Tests.Helpers;

public sealed class InMemoryRepository<T> : IRepository<T> where T : BaseEntity
{
    private readonly List<T> _items = [];
    private int _nextId = 1;

    public IReadOnlyList<T> Items => _items;

    public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(_items.FirstOrDefault(e => e.Id == id));

    public Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IEnumerable<T>>(_items.ToList());

    public Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        Task.FromResult(_items.Where(predicate.Compile()));

    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        Task.FromResult(_items.FirstOrDefault(predicate.Compile()));

    public Task AddAsync(T entity, CancellationToken ct = default)
    {
        if (entity.Id == 0)
            entity.Id = _nextId++;

        _items.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(T entity)
    {
        var index = _items.FindIndex(e => e.Id == entity.Id);
        if (index >= 0)
            _items[index] = entity;
    }

    public void Remove(T entity) => _items.RemoveAll(e => e.Id == entity.Id);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        Task.FromResult(_items.Any(predicate.Compile()));

    public Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        Task.FromResult(_items.Count(predicate.Compile()));

    public void Seed(params T[] entities)
    {
        foreach (var entity in entities)
        {
            if (entity.Id == 0)
                entity.Id = _nextId++;
            else if (entity.Id >= _nextId)
                _nextId = entity.Id + 1;

            _items.Add(entity);
        }
    }
}
