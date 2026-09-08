using AccountApi.Core;
using AccountApi.Core.Exceptions;

namespace AccountApi.Tests.Unit;

/// <summary>
/// Exercises the service against the real in-memory store so that every
/// assertion reflects actual persisted state, not a mocked return value.
/// </summary>
public class AccountServiceTests
{
    private readonly InMemoryAccountStore _store = new();
    private readonly AccountService _service;

    public AccountServiceTests()
    {
        _service = new AccountService(_store);
    }

    // --- Balance -----------------------------------------------------------

    [Fact]
    public void GetBalance_UnknownAccount_Throws()
    {
        Assert.Throws<AccountNotFoundException>(() => _service.GetBalance("100"));
    }

    [Fact]
    public void GetBalance_DoesNotCreateAccount()
    {
        Assert.Throws<AccountNotFoundException>(() => _service.GetBalance("100"));

        // Reading a second time must still fail: the first read had no side effect.
        Assert.Throws<AccountNotFoundException>(() => _service.GetBalance("100"));
        Assert.Null(_store.Find("100"));
    }

    [Fact]
    public void GetBalance_DoesNotChangeBalance()
    {
        _service.Deposit("100", 10m);

        var first = _service.GetBalance("100");
        var second = _service.GetBalance("100");

        Assert.Equal(10m, first);
        Assert.Equal(10m, second);
        Assert.Equal(10m, _store.Find("100")!.Balance);
    }

    // --- Deposit -----------------------------------------------------------

    [Fact]
    public void Deposit_NewAccount_CreatesItWithAmount()
    {
        var result = _service.Deposit("100", 10m);

        Assert.Equal(new AccountBalance("100", 10m), result);
        Assert.Equal(10m, _store.Find("100")!.Balance);
    }

    [Fact]
    public void Deposit_ExistingAccount_AccumulatesBalance()
    {
        _service.Deposit("100", 10m);

        var result = _service.Deposit("100", 10m);

        Assert.Equal(20m, result.Balance);
        Assert.Equal(20m, _service.GetBalance("100"));
    }

    [Fact]
    public void Deposit_InvalidAmount_ThrowsAndDoesNotCreateAccount()
    {
        Assert.Throws<InvalidAmountException>(() => _service.Deposit("100", 0m));

        Assert.Null(_store.Find("100"));
    }

    // --- Withdraw ----------------------------------------------------------

    [Fact]
    public void Withdraw_ReducesBalance()
    {
        _service.Deposit("100", 20m);

        var result = _service.Withdraw("100", 5m);

        Assert.Equal(15m, result.Balance);
        Assert.Equal(15m, _service.GetBalance("100"));
    }

    [Fact]
    public void Withdraw_UnknownAccount_ThrowsAndDoesNotCreateAccount()
    {
        Assert.Throws<AccountNotFoundException>(() => _service.Withdraw("100", 5m));

        Assert.Null(_store.Find("100"));
    }

    [Fact]
    public void Withdraw_InsufficientFunds_ThrowsAndKeepsBalance()
    {
        _service.Deposit("100", 10m);

        Assert.Throws<InsufficientFundsException>(() => _service.Withdraw("100", 11m));

        Assert.Equal(10m, _service.GetBalance("100"));
    }

    // --- Transfer ----------------------------------------------------------

    [Fact]
    public void Transfer_MovesFundsBetweenExistingAccounts()
    {
        _service.Deposit("100", 15m);
        _service.Deposit("300", 5m);

        var result = _service.Transfer("100", "300", 15m);

        Assert.Equal(new AccountBalance("100", 0m), result.Origin);
        Assert.Equal(new AccountBalance("300", 20m), result.Destination);
        Assert.Equal(0m, _service.GetBalance("100"));
        Assert.Equal(20m, _service.GetBalance("300"));
    }

    [Fact]
    public void Transfer_ToUnknownDestination_CreatesIt()
    {
        _service.Deposit("100", 15m);

        var result = _service.Transfer("100", "300", 15m);

        Assert.Equal(0m, result.Origin.Balance);
        Assert.Equal(15m, result.Destination.Balance);
        Assert.Equal(15m, _service.GetBalance("300"));
    }

    [Fact]
    public void Transfer_FromUnknownOrigin_ThrowsAndTouchesNothing()
    {
        _service.Deposit("300", 5m);

        Assert.Throws<AccountNotFoundException>(() => _service.Transfer("100", "300", 5m));

        Assert.Null(_store.Find("100"));
        Assert.Equal(5m, _service.GetBalance("300"));
    }

    [Fact]
    public void Transfer_InsufficientFunds_LeavesBothBalancesIntact()
    {
        _service.Deposit("100", 10m);
        _service.Deposit("300", 5m);

        Assert.Throws<InsufficientFundsException>(() => _service.Transfer("100", "300", 11m));

        Assert.Equal(10m, _service.GetBalance("100"));
        Assert.Equal(5m, _service.GetBalance("300"));
    }

    [Fact]
    public void Transfer_InsufficientFunds_DoesNotCreateDestination()
    {
        _service.Deposit("100", 10m);

        Assert.Throws<InsufficientFundsException>(() => _service.Transfer("100", "300", 11m));

        Assert.Null(_store.Find("300"));
    }

    [Fact]
    public void Transfer_InvalidAmount_LeavesBothBalancesIntact()
    {
        _service.Deposit("100", 10m);
        _service.Deposit("300", 5m);

        Assert.Throws<InvalidAmountException>(() => _service.Transfer("100", "300", -1m));

        Assert.Equal(10m, _service.GetBalance("100"));
        Assert.Equal(5m, _service.GetBalance("300"));
    }

    // --- Reset -------------------------------------------------------------

    [Fact]
    public void Reset_RemovesAllAccounts()
    {
        _service.Deposit("100", 10m);
        _service.Deposit("300", 5m);

        _service.Reset();

        Assert.Throws<AccountNotFoundException>(() => _service.GetBalance("100"));
        Assert.Throws<AccountNotFoundException>(() => _service.GetBalance("300"));
    }

    [Fact]
    public void Reset_AllowsAccountsToBeRecreatedFromZero()
    {
        _service.Deposit("100", 10m);
        _service.Reset();

        var result = _service.Deposit("100", 3m);

        Assert.Equal(3m, result.Balance);
    }
}
