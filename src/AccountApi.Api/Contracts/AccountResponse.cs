using AccountApi.Core;

namespace AccountApi.Api.Contracts;

public sealed record AccountResponse(string Id, decimal Balance)
{
    public static AccountResponse From(AccountBalance balance) => new(balance.Id, balance.Balance);
}
