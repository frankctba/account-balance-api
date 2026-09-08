using AccountApi.Core.Exceptions;

namespace AccountApi.Core;

/// <summary>
/// Business operations over accounts. Every operation runs under a single lock so
/// that multi-step changes (transfers) are atomic and no caller ever observes an
/// intermediate state. Methods return snapshots taken inside the lock, so the
/// returned balances are exactly the ones produced by that operation.
/// </summary>
public sealed class AccountService(IAccountStore store)
{
    private readonly object _sync = new();

    /// <summary>Read-only query: never creates or mutates an account.</summary>
    public decimal GetBalance(string accountId)
    {
        lock (_sync)
        {
            var account = store.Find(accountId) ?? throw new AccountNotFoundException(accountId);
            return account.Balance;
        }
    }

    /// <summary>Credits the destination, creating the account on first deposit.</summary>
    public AccountBalance Deposit(string destinationId, decimal amount)
    {
        lock (_sync)
        {
            var destination = store.Find(destinationId) ?? new Account(destinationId);
            destination.Deposit(amount);
            store.Save(destination);
            return destination.Snapshot();
        }
    }

    /// <summary>Debits the origin. Fails if the account is missing or underfunded.</summary>
    public AccountBalance Withdraw(string originId, decimal amount)
    {
        lock (_sync)
        {
            var origin = store.Find(originId) ?? throw new AccountNotFoundException(originId);
            origin.Withdraw(amount);
            store.Save(origin);
            return origin.Snapshot();
        }
    }

    /// <summary>
    /// Moves funds between accounts atomically. All validation happens before
    /// any balance changes: if the origin is missing or underfunded, neither
    /// side is touched. The destination is created if it does not exist yet.
    /// </summary>
    public TransferResult Transfer(string originId, string destinationId, decimal amount)
    {
        lock (_sync)
        {
            var origin = store.Find(originId) ?? throw new AccountNotFoundException(originId);
            var destination = store.Find(destinationId) ?? new Account(destinationId);

            // Withdraw validates amount and funds and throws before mutating.
            // Once it succeeds, Deposit of the same positive amount cannot fail.
            origin.Withdraw(amount);
            destination.Deposit(amount);

            store.Save(origin);
            store.Save(destination);
            return new TransferResult(origin.Snapshot(), destination.Snapshot());
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            store.Clear();
        }
    }
}
