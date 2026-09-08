using AccountApi.Core;
using AccountApi.Core.Exceptions;

namespace AccountApi.Tests.Unit;

/// <summary>
/// Hammers the service from many threads to prove that the single lock keeps
/// balances exact and that money is neither created nor lost in transfers.
/// </summary>
public class AccountServiceConcurrencyTests
{
    private readonly AccountService _service = new(new InMemoryAccountStore());

    [Fact]
    public void ParallelDeposits_SumExactly()
    {
        const int deposits = 1_000;

        Parallel.For(0, deposits, _ => _service.Deposit("100", 1m));

        Assert.Equal(deposits, _service.GetBalance("100"));
    }

    [Fact]
    public void ParallelWithdrawals_NeverOverdraw()
    {
        const int attempts = 200;
        const int funds = 100;
        _service.Deposit("100", funds);

        var succeeded = 0;
        var rejected = 0;
        Parallel.For(0, attempts, _ =>
        {
            try
            {
                _service.Withdraw("100", 1m);
                Interlocked.Increment(ref succeeded);
            }
            catch (InsufficientFundsException)
            {
                Interlocked.Increment(ref rejected);
            }
        });

        Assert.Equal(funds, succeeded);
        Assert.Equal(attempts - funds, rejected);
        Assert.Equal(0m, _service.GetBalance("100"));
    }

    [Fact]
    public void ParallelTransfersInBothDirections_PreserveTotalFunds()
    {
        const int transfers = 1_000;
        _service.Deposit("A", 500m);
        _service.Deposit("B", 500m);

        Parallel.For(0, transfers, i =>
        {
            var (origin, destination) = i % 2 == 0 ? ("A", "B") : ("B", "A");
            try
            {
                _service.Transfer(origin, destination, 3m);
            }
            catch (InsufficientFundsException)
            {
                // Expected when one side momentarily runs dry; state must still be consistent.
            }
        });

        var a = _service.GetBalance("A");
        var b = _service.GetBalance("B");

        Assert.Equal(1_000m, a + b);
        Assert.True(a >= 0m && b >= 0m);
    }
}
