using AccountApi.Core.Exceptions;

namespace AccountApi.Api.Endpoints;

/// <summary>
/// Single place where business rule violations become HTTP responses.
/// Anything that is not a <see cref="DomainException"/> propagates and is
/// handled by the framework as an unexpected error (500).
/// </summary>
public sealed class DomainExceptionFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (AccountNotFoundException)
        {
            // The API contract mandates a literal 0 body for unknown accounts.
            return Results.Json(0, statusCode: StatusCodes.Status404NotFound);
        }
        catch (InsufficientFundsException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity);
        }
        catch (InvalidAmountException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
