using AccountApi.Core;
using AccountApi.Core.Exceptions;

namespace AccountApi.Tests.Unit;

/// <summary>
/// Hammers the service from many threads to prove that the single lock keeps
/// balances exact and that money is neither created nor lost in transfers.
///
/// <para>
/// Why <see cref="Parallel.For(int, int, Action{int})"/>: it spreads the iterations
/// across thread-pool threads and blocks until all of them finish. This mirrors what
/// the web server does in production, where concurrent HTTP requests run on separate
/// threads and all hit the same singleton <see cref="AccountService"/>. A sequential
/// loop would pass even with the lock removed and therefore prove nothing.
/// </para>
///
/// <para>
/// These tests are probabilistic. They cannot prove the absence of a race, but with
/// hundreds of operations contending for the same account, a missing lock makes a
/// failure almost certain. The formal guarantee comes from reading the service: one
/// lock wraps read, validate and write. These tests are the empirical evidence.
/// </para>
///
/// <para>
/// Note that the <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey, TValue}"/>
/// in the store does not make these tests pass on its own. It protects the map, not the
/// mutable <see cref="Account"/> stored inside it. Read-modify-write on the balance still
/// needs the lock in the service.
/// </para>
/// </summary>
public class AccountServiceConcurrencyTests
{
    private readonly AccountService _service = new(new InMemoryAccountStore());

    /// <summary>
    /// Detects lost updates. Deposit is read, add, write. Without the lock, two threads
    /// can read the same balance, both add 1 and both write the same result, silently
    /// dropping one deposit. The final balance would come out short by a varying amount.
    /// With the lock, every deposit is applied exactly once and the sum is exact.
    /// </summary>
    [Fact]
    public void ParallelDeposits_SumExactly()
    {
        const int deposits = 1_000;

        Parallel.For(0, deposits, _ => _service.Deposit("100", 1m));

        Assert.Equal(deposits, _service.GetBalance("100"));
    }

    /// <summary>
    /// Detects check-then-act races. Withdraw compares the balance to the amount and
    /// then subtracts. Without the lock, two threads can both see a balance of 1, both
    /// pass the check and both subtract, leaving the account negative. The lock makes
    /// the check and the subtraction one indivisible step.
    ///
    /// <para>
    /// Twice as many withdrawals are attempted as the account can afford, so half must
    /// succeed and half must be rejected. All three assertions matter: counting
    /// successes and rejections closes the books from both sides, and the final zero
    /// balance confirms that exactly the available funds were withdrawn.
    /// </para>
    ///
    /// <para>
    /// The counters are themselves shared between threads, so they are incremented
    /// with <see cref="Interlocked.Increment(ref int)"/>. A plain <c>++</c> would suffer the
    /// same lost-update problem this test is trying to detect in the service.
    /// </para>
    /// </summary>
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

    /// <summary>
    /// Detects non-atomic transfers. Money is moved back and forth between two accounts
    /// from many threads at once. If the debit and the credit were not applied together,
    /// a concurrent reader or writer could observe or act on a state where the funds are
    /// in neither account or in both, and the total would drift.
    ///
    /// <para>
    /// Some transfers are expected to fail with insufficient funds when one side is
    /// momentarily drained. That is fine: the invariant under test is conservation.
    /// Whatever the interleaving, the two balances must still add up to the initial
    /// total and neither may be negative.
    /// </para>
    /// </summary>
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
