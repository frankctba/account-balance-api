using System.Text.Json.Serialization;
using AccountApi.Api.Endpoints;
using AccountApi.Core;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

// One store and one service for the process lifetime: state lives in memory.
builder.Services.AddSingleton<IAccountStore, InMemoryAccountStore>();
builder.Services.AddSingleton<AccountService>();

// Every error response, including framework-generated ones, is an RFC 9457 problem details body.
builder.Services.AddProblemDetails();
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Omit sides not involved in an event instead of serializing them as null.
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    // Amounts must be JSON numbers; "10" as a string is rejected.
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapAccountEndpoints();

app.Run();

// Exposes the entry point to the integration tests (WebApplicationFactory<Program>).
public partial class Program;
