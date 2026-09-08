namespace AccountApi.Core.Exceptions;

public sealed class AccountNotFoundException(string accountId)
    : DomainException($"Account '{accountId}' was not found.")
{
    public string AccountId { get; } = accountId;
}
