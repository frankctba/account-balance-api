using System.Collections.Concurrent;

namespace AccountApi.Core;

/// <summary>
/// Volatile store backed by a concurrent dictionary. Each single operation is
/// thread-safe on its own; multi-step consistency (e.g. transfers) is the
/// responsibility of <see cref="AccountService"/>.
/// </summary>
public sealed class InMemoryAccountStore : IAccountStore
{
    private readonly ConcurrentDictionary<string, Account> _accounts = new();

    public Account? Find(string id) =>
        _accounts.TryGetValue(id, out var account) ? account : null;

    public void Save(Account account) => _accounts[account.Id] = account;

    public void Clear() => _accounts.Clear();
}
