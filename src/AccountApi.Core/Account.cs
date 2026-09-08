using AccountApi.Core.Exceptions;

namespace AccountApi.Core;

/// <summary>
/// A bank account identified by an opaque string id. The entity owns the
/// invariants: amounts are always positive and the balance never goes negative.
/// </summary>
public sealed class Account
{
    public string Id { get; }
    public decimal Balance { get; private set; }

    public Account(string id, decimal balance = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
        Balance = balance;
    }

    public void Deposit(decimal amount)
    {
        EnsurePositive(amount);
        Balance += amount;
    }

    public void Withdraw(decimal amount)
    {
        EnsurePositive(amount);
        if (amount > Balance)
        {
            throw new InsufficientFundsException(Id, Balance, amount);
        }

        Balance -= amount;
    }

    /// <summary>Immutable copy of the current state, safe to hand out of a lock.</summary>
    public AccountBalance Snapshot() => new(Id, Balance);

    private static void EnsurePositive(decimal amount)
    {
        if (amount <= 0)
        {
            throw new InvalidAmountException(amount);
        }
    }
}
