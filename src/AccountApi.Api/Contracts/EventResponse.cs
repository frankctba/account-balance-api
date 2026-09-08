namespace AccountApi.Api.Contracts;

/// <summary>
/// Uniform response for every event type. Sides not involved in the event are
/// omitted from the JSON (nulls are not serialized).
/// </summary>
public sealed record EventResponse(AccountResponse? Origin = null, AccountResponse? Destination = null);
