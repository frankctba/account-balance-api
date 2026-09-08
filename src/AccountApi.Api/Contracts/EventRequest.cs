namespace AccountApi.Api.Contracts;

/// <summary>Body of POST /event. Which fields are required depends on <see cref="Type"/>.</summary>
public sealed record EventRequest(string? Type, string? Origin, string? Destination, decimal? Amount);
