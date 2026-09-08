using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AccountApi.Tests.Integration;

/// <summary>
/// End-to-end tests over HTTP against the real application (in-memory host).
/// Each test gets its own host, so state never leaks between tests.
/// Bodies are compared literally because the response format is part of the contract.
/// </summary>
public sealed class AccountApiTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory = new();
    private readonly HttpClient _client;

    public AccountApiTests()
    {
        _client = _factory.CreateClient();
    }

    public void Dispose() => _factory.Dispose();

    // --- Full flow -----------------------------------------------------------

    [Fact]
    public async Task FullFlow_ResetDepositBalanceWithdrawTransfer()
    {
        await AssertResponse(await _client.PostAsync("/reset", null), HttpStatusCode.OK, "OK");

        await AssertResponse(await _client.GetAsync("/balance?account_id=1234"), HttpStatusCode.NotFound, "0");

        await AssertResponse(
            await PostEvent("""{"type":"deposit","destination":"100","amount":10}"""),
            HttpStatusCode.Created,
            """{"destination":{"id":"100","balance":10}}""");

        await AssertResponse(
            await PostEvent("""{"type":"deposit","destination":"100","amount":10}"""),
            HttpStatusCode.Created,
            """{"destination":{"id":"100","balance":20}}""");

        await AssertResponse(await _client.GetAsync("/balance?account_id=100"), HttpStatusCode.OK, "20");

        await AssertResponse(
            await PostEvent("""{"type":"withdraw","origin":"200","amount":10}"""),
            HttpStatusCode.NotFound,
            "0");

        await AssertResponse(
            await PostEvent("""{"type":"withdraw","origin":"100","amount":5}"""),
            HttpStatusCode.Created,
            """{"origin":{"id":"100","balance":15}}""");

        await AssertResponse(
            await PostEvent("""{"type":"transfer","origin":"100","amount":15,"destination":"300"}"""),
            HttpStatusCode.Created,
            """{"origin":{"id":"100","balance":0},"destination":{"id":"300","balance":15}}""");

        await AssertResponse(
            await PostEvent("""{"type":"transfer","origin":"200","amount":15,"destination":"300"}"""),
            HttpStatusCode.NotFound,
            "0");

        await AssertResponse(await _client.GetAsync("/balance?account_id=100"), HttpStatusCode.OK, "0");
        await AssertResponse(await _client.GetAsync("/balance?account_id=300"), HttpStatusCode.OK, "15");
    }

    // --- GET has no side effects -------------------------------------------

    [Fact]
    public async Task Balance_UnknownAccount_DoesNotCreateIt()
    {
        await AssertResponse(await _client.GetAsync("/balance?account_id=100"), HttpStatusCode.NotFound, "0");
        await AssertResponse(await _client.GetAsync("/balance?account_id=100"), HttpStatusCode.NotFound, "0");

        // The first real deposit must start from zero, not from a phantom account.
        await AssertResponse(
            await PostEvent("""{"type":"deposit","destination":"100","amount":7}"""),
            HttpStatusCode.Created,
            """{"destination":{"id":"100","balance":7}}""");
    }

    [Fact]
    public async Task Balance_RepeatedReads_ReturnSameValue()
    {
        await PostEvent("""{"type":"deposit","destination":"100","amount":10}""");

        for (var i = 0; i < 3; i++)
        {
            await AssertResponse(await _client.GetAsync("/balance?account_id=100"), HttpStatusCode.OK, "10");
        }
    }

    [Fact]
    public async Task Balance_MissingAccountId_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/balance");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("account_id", await response.Content.ReadAsStringAsync());
    }

    // --- Error cases keep state intact ---------------------------------------

    [Fact]
    public async Task Withdraw_InsufficientFunds_Returns422AndKeepsBalance()
    {
        await PostEvent("""{"type":"deposit","destination":"100","amount":10}""");

        var response = await PostEvent("""{"type":"withdraw","origin":"100","amount":11}""");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        await AssertResponse(await _client.GetAsync("/balance?account_id=100"), HttpStatusCode.OK, "10");
    }

    [Fact]
    public async Task Transfer_InsufficientFunds_Returns422AndKeepsBothBalances()
    {
        await PostEvent("""{"type":"deposit","destination":"100","amount":10}""");
        await PostEvent("""{"type":"deposit","destination":"300","amount":5}""");

        var response = await PostEvent("""{"type":"transfer","origin":"100","destination":"300","amount":11}""");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        await AssertResponse(await _client.GetAsync("/balance?account_id=100"), HttpStatusCode.OK, "10");
        await AssertResponse(await _client.GetAsync("/balance?account_id=300"), HttpStatusCode.OK, "5");
    }

    [Fact]
    public async Task Transfer_UnknownOrigin_Returns404AndDoesNotCreditDestination()
    {
        await PostEvent("""{"type":"deposit","destination":"300","amount":5}""");

        await AssertResponse(
            await PostEvent("""{"type":"transfer","origin":"100","destination":"300","amount":5}"""),
            HttpStatusCode.NotFound,
            "0");

        await AssertResponse(await _client.GetAsync("/balance?account_id=300"), HttpStatusCode.OK, "5");
        await AssertResponse(await _client.GetAsync("/balance?account_id=100"), HttpStatusCode.NotFound, "0");
    }

    // --- Input validation ----------------------------------------------------

    [Theory]
    [InlineData("""{"type":"deposit","destination":"100","amount":0}""", "greater than zero")]
    [InlineData("""{"type":"deposit","destination":"100","amount":-5}""", "greater than zero")]
    [InlineData("""{"type":"deposit","destination":"100"}""", "amount")]
    [InlineData("""{"type":"deposit","amount":5}""", "destination")]
    [InlineData("""{"type":"withdraw","amount":5}""", "origin")]
    [InlineData("""{"type":"transfer","origin":"100","amount":5}""", "destination")]
    [InlineData("""{"type":"refund","destination":"100","amount":5}""", "type")]
    [InlineData("""{"destination":"100","amount":5}""", "type")]
    public async Task Event_InvalidRequest_ReturnsBadRequestWithReason(string body, string expectedInDetail)
    {
        var response = await PostEvent(body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expectedInDetail, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("""{"type":"deposit","destination":"100","amount":"10"}""")]
    public async Task Event_MalformedBody_ReturnsBadRequest(string body)
    {
        var response = await PostEvent(body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Event_InvalidRequest_DoesNotChangeState()
    {
        await PostEvent("""{"type":"deposit","destination":"100","amount":10}""");

        await PostEvent("""{"type":"deposit","destination":"100","amount":-1}""");
        await PostEvent("""{"type":"withdraw","origin":"100"}""");
        await PostEvent("""{"type":"bogus","origin":"100","amount":1}""");

        await AssertResponse(await _client.GetAsync("/balance?account_id=100"), HttpStatusCode.OK, "10");
    }

    // --- Reset ---------------------------------------------------------------

    [Fact]
    public async Task Reset_ClearsAllAccounts()
    {
        await PostEvent("""{"type":"deposit","destination":"100","amount":10}""");
        await PostEvent("""{"type":"deposit","destination":"300","amount":5}""");

        await AssertResponse(await _client.PostAsync("/reset", null), HttpStatusCode.OK, "OK");

        await AssertResponse(await _client.GetAsync("/balance?account_id=100"), HttpStatusCode.NotFound, "0");
        await AssertResponse(await _client.GetAsync("/balance?account_id=300"), HttpStatusCode.NotFound, "0");
    }

    // --- Helpers -------------------------------------------------------------

    private Task<HttpResponseMessage> PostEvent(string json) =>
        _client.PostAsync("/event", new StringContent(json, Encoding.UTF8, "application/json"));

    private static async Task AssertResponse(HttpResponseMessage response, HttpStatusCode expectedStatus, string expectedBody)
    {
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(expectedBody, body);
    }
}
