using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

public class PaymentProcessingTests
{
    [Theory]
    [InlineData(true, "Authorized")]
    [InlineData(false, "Declined")]
    public async Task CreatesPaymentAndRetrievesSameSafeDetails(bool authorized, string status)
    {
        string? bankBody = null;
        using var bank = new RecordingBankHandler
        {
            Respond = async (request, ct) =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("/payments", request.RequestUri!.AbsolutePath);
                bankBody = await request.Content!.ReadAsStringAsync(ct);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { authorized, authorization_code = "bank-auth" })
                };
            }
        };
        using var factory = PaymentValidationTests.CreateFactory(bank);
        using var client = factory.CreateClient();
        var request = PaymentValidationTests.ValidRequest();
        request["cardNumber"] = "2222405343200007";

        using var response = await client.PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payment = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = payment.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(status, payment.GetProperty("status").GetString());
        Assert.Equal("0007", payment.GetProperty("cardNumberLastFour").GetString());
        Assert.Equal(4, payment.GetProperty("expiryMonth").GetInt32());
        Assert.Equal(2031, payment.GetProperty("expiryYear").GetInt32());
        Assert.Equal("GBP", payment.GetProperty("currency").GetString());
        Assert.Equal(1050, payment.GetProperty("amount").GetInt32());
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith($"/api/Payments/{id}", response.Headers.Location!.ToString());
        using var retrieved = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, retrieved.StatusCode);
        Assert.Equal(payment.GetRawText(), await retrieved.Content.ReadAsStringAsync());
        Assert.DoesNotContain("2222405343200007", payment.GetRawText());
        Assert.False(payment.TryGetProperty("cvv", out _));
        Assert.False(payment.TryGetProperty("authorizationCode", out _));
        Assert.Equal(1, bank.Calls);
        using var sent = JsonDocument.Parse(bankBody!);
        Assert.Equal("2222405343200007", sent.RootElement.GetProperty("card_number").GetString());
        Assert.Equal("04/2031", sent.RootElement.GetProperty("expiry_date").GetString());
        Assert.Equal("GBP", sent.RootElement.GetProperty("currency").GetString());
        Assert.Equal(1050, sent.RootElement.GetProperty("amount").GetInt32());
        Assert.Equal("987", sent.RootElement.GetProperty("cvv").GetString());
        Assert.Equal(5, sent.RootElement.EnumerateObject().Count());
    }

    [Theory]
    [InlineData("GBP", "12345678901234", "123", 1, 1, 2031)]
    [InlineData("USD", "1234567890123456789", "0123", 2147483647, 12, 2031)]
    [InlineData("EUR", "1234567890123456", "001", 100, 7, 2030)]
    public async Task AcceptsValidBoundaries(string currency, string card, string cvv, int amount, int month, int year)
    {
        using var bank = new RecordingBankHandler();
        using var factory = PaymentValidationTests.CreateFactory(bank);
        using var client = factory.CreateClient();
        var request = PaymentValidationTests.ValidRequest();
        request["currency"] = currency;
        request["cardNumber"] = card;
        request["cvv"] = cvv;
        request["amount"] = amount;
        request["expiryMonth"] = month;
        request["expiryYear"] = year;

        using var response = await client.PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(1, bank.Calls);
    }

    [Theory]
    [InlineData(503, "{}", 503)]
    [InlineData(500, "bank internal details", 502)]
    [InlineData(400, "bank internal details", 502)]
    [InlineData(200, "not json", 502)]
    [InlineData(200, "{}", 502)]
    [InlineData(200, "null", 502)]
    [InlineData(200, "{\"authorized\": \"yes\"}", 502)]
    [InlineData(200, "{\"authorized\": null}", 502)]
    public async Task BankFailureDoesNotCreatePaymentOrRetry(int bankStatus, string body, int expectedStatus)
    {
        using var bank = new RecordingBankHandler
        {
            Respond = (_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)bankStatus)
            {
                Content = new StringContent(body)
            })
        };
        using var factory = PaymentValidationTests.CreateFactory(bank);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/Payments", PaymentValidationTests.ValidRequest());

        Assert.Equal((HttpStatusCode)expectedStatus, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expectedStatus, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
        Assert.False(problem.TryGetProperty("id", out _));
        Assert.DoesNotContain("bank internal details", problem.GetRawText());
        Assert.Empty(factory.Services.GetRequiredService<PaymentsRepository>().Payments);
        Assert.Equal(1, bank.Calls);
    }

    [Theory]
    [InlineData(false, 503)]
    [InlineData(true, 504)]
    public async Task ConnectionFailureOrTimeoutHasNoInventedOutcome(bool timeout, int expectedStatus)
    {
        using var bank = new RecordingBankHandler
        {
            Respond = (_, _) => throw (timeout
                ? new TaskCanceledException("Sensitive upstream details")
                : new HttpRequestException("Sensitive upstream details"))
        };
        using var factory = PaymentValidationTests.CreateFactory(bank);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/Payments", PaymentValidationTests.ValidRequest());

        Assert.Equal((HttpStatusCode)expectedStatus, response.StatusCode);
        Assert.DoesNotContain("Sensitive upstream details", await response.Content.ReadAsStringAsync());
        Assert.Empty(factory.Services.GetRequiredService<PaymentsRepository>().Payments);
        Assert.Equal(1, bank.Calls);
    }
}