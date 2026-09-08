namespace AccountApi.Core;

/// <summary>Both sides of a transfer, captured atomically right after it completed.</summary>
public sealed record TransferResult(AccountBalance Origin, AccountBalance Destination);
