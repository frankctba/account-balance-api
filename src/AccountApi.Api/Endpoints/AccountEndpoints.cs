using AccountApi.Api.Contracts;
using AccountApi.Core;
using Microsoft.AspNetCore.Mvc;

namespace AccountApi.Api.Endpoints;

public static class AccountEndpoints
{
    private const string DepositType = "deposit";
    private const string WithdrawType = "withdraw";
    private const string TransferType = "transfer";

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(string.Empty).AddEndpointFilter<DomainExceptionFilter>();

        group.MapPost("/reset", Reset);
        group.MapGet("/balance", GetBalance);
        group.MapPost("/event", HandleEvent);

        return app;
    }

    private static IResult Reset(AccountService service)
    {
        service.Reset();
        return Results.Text("OK");
    }

    private static IResult GetBalance([FromQuery(Name = "account_id")] string? accountId, AccountService service)
    {
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return BadRequest("Query parameter 'account_id' is required.");
        }

        return Results.Ok(service.GetBalance(accountId));
    }

    private static IResult HandleEvent(EventRequest request, AccountService service)
    {
        if (request.Amount is not decimal amount)
        {
            return BadRequest("Field 'amount' is required.");
        }

        switch (request.Type)
        {
            case DepositType:
                if (string.IsNullOrWhiteSpace(request.Destination))
                {
                    return BadRequest($"Field 'destination' is required for '{DepositType}'.");
                }

                var deposited = service.Deposit(request.Destination, amount);
                return Created(new EventResponse(Destination: AccountResponse.From(deposited)));

            case WithdrawType:
                if (string.IsNullOrWhiteSpace(request.Origin))
                {
                    return BadRequest($"Field 'origin' is required for '{WithdrawType}'.");
                }

                var withdrawn = service.Withdraw(request.Origin, amount);
                return Created(new EventResponse(Origin: AccountResponse.From(withdrawn)));

            case TransferType:
                if (string.IsNullOrWhiteSpace(request.Origin) || string.IsNullOrWhiteSpace(request.Destination))
                {
                    return BadRequest($"Fields 'origin' and 'destination' are required for '{TransferType}'.");
                }

                var transfer = service.Transfer(request.Origin, request.Destination, amount);
                return Created(new EventResponse(
                    Origin: AccountResponse.From(transfer.Origin),
                    Destination: AccountResponse.From(transfer.Destination)));

            default:
                return BadRequest($"Field 'type' must be one of: {DepositType}, {WithdrawType}, {TransferType}.");
        }
    }

    private static IResult Created(EventResponse response) =>
        Results.Json(response, statusCode: StatusCodes.Status201Created);

    private static IResult BadRequest(string detail) =>
        Results.Problem(detail, statusCode: StatusCodes.Status400BadRequest);
}
