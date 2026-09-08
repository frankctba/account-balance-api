namespace AccountApi.Core;

/// <summary>Read-only view of an account at a point in time.</summary>
public sealed record AccountBalance(string Id, decimal Balance);
