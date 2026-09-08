namespace AccountApi.Core;

/// <summary>
/// Persistence boundary for accounts. The only implementation today is in-memory;
/// a database-backed store would implement the same three operations.
/// </summary>
public interface IAccountStore
{
    Account? Find(string id);

    /// <summary>Inserts or replaces the account identified by <see cref="Account.Id"/>.</summary>
    void Save(Account account);

    void Clear();
}
