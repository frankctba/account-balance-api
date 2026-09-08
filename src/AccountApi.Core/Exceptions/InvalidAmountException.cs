namespace AccountApi.Core.Exceptions;

public sealed class InvalidAmountException(decimal amount)
    : DomainException($"Amount must be greater than zero, but was {amount}.")
{
    public decimal Amount { get; } = amount;
}
