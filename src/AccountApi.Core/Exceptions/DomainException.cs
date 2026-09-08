namespace AccountApi.Core.Exceptions;

/// <summary>
/// Base type for business rule violations. The HTTP layer maps each
/// concrete subtype to a status code in a single place.
/// </summary>
public abstract class DomainException(string message) : Exception(message);
