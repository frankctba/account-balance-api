namespace AccountApi.Core.Exceptions;

public sealed class InsufficientFundsException(string accountId, decimal balance, decimal requested)
    : DomainException($"Account '{accountId}' has balance {balance} but {requested} was requested.")
{
    public string AccountId { get; } = accountId;
    public decimal Balance { get; } = balance;
    public decimal Requested { get; } = requested;
}
