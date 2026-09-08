using AccountApi.Core;
using AccountApi.Core.Exceptions;

namespace AccountApi.Tests.Unit;

public class AccountTests
{
    [Fact]
    public void NewAccount_StartsWithZeroBalance()
    {
        var account = new Account("100");

        Assert.Equal("100", account.Id);
        Assert.Equal(0m, account.Balance);
    }

    [Fact]
    public void Deposit_IncreasesBalance()
    {
        var account = new Account("100");

        account.Deposit(10m);
        account.Deposit(2.5m);

        Assert.Equal(12.5m, account.Balance);
    }

    [Fact]
    public void Withdraw_DecreasesBalance()
    {
        var account = new Account("100", 20m);

        account.Withdraw(5m);

        Assert.Equal(15m, account.Balance);
    }

    [Fact]
    public void Withdraw_ExactBalance_LeavesZero()
    {
        var account = new Account("100", 20m);

        account.Withdraw(20m);

        Assert.Equal(0m, account.Balance);
    }

    [Fact]
    public void Withdraw_MoreThanBalance_ThrowsAndKeepsBalance()
    {
        var account = new Account("100", 10m);

        var ex = Assert.Throws<InsufficientFundsException>(() => account.Withdraw(10.01m));

        Assert.Equal(10m, account.Balance);
        Assert.Equal(10m, ex.Balance);
        Assert.Equal(10.01m, ex.Requested);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Deposit_NonPositiveAmount_ThrowsAndKeepsBalance(decimal amount)
    {
        var account = new Account("100", 10m);

        Assert.Throws<InvalidAmountException>(() => account.Deposit(amount));

        Assert.Equal(10m, account.Balance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Withdraw_NonPositiveAmount_ThrowsAndKeepsBalance(decimal amount)
    {
        var account = new Account("100", 10m);

        Assert.Throws<InvalidAmountException>(() => account.Withdraw(amount));

        Assert.Equal(10m, account.Balance);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankId_Throws(string id)
    {
        Assert.Throws<ArgumentException>(() => new Account(id));
    }
}
