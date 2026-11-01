using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using PaymentGateway.Api.Controllers;

namespace PaymentGateway.Api.Tests;

public class PaymentValidationTests
{
    public static IEnumerable<object[]> InvalidRequests()
    {
        foreach (var field in ValidRequest().Keys)
        {
            var missing = ValidRequest();
            missing.Remove(field);
            yield return new object[] { $"missing {field}", missing };
            var nullValue = ValidRequest();
            nullValue[field] = null;
            yield return new object[] { $"null {field}", nullValue };
        }

        var invalidValues = new Dictionary<string, object[]>
        {
            ["cardNumber"] = new object[] { "", "1234567890123", "12345678901234567890", "1234567890123a", "1234 5678901234", "１２３４５６７８９０１２３４", 12345678901234L },
            ["expiryMonth"] = new object[] { 0, -1, 13, 1.5, "04" },
            ["expiryYear"] = new object[] { 2029, 2030, 10000, 2031.5 },
            ["currency"] = new object[] { "", "GB", "GBPP", "JPY", "gbp", " GBP" },
            ["amount"] = new object[] { 0, -1, 1.5, "100", 2147483648L },
            ["cvv"] = new object[] { "", "12", "12345", "1a3", " 12", "１２３", 123 }
        };
        foreach (var (field, values) in invalidValues)
        {
            foreach (var value in values)
            {
                var request = ValidRequest();
                request[field] = value;
                yield return new object[] { $"invalid {field}: {value}", request };
            }
        }
        foreach (var month in new[] { 5, 6 })
        {
            var expired = ValidRequest();
            expired["expiryMonth"] = month;
            expired["expiryYear"] = 2030;
            yield return new object[] { $"past or current month in current year: {month}", expired };
        }
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task given_an_invalid_payment_request_when_submitted_then_rejects_without_calling_bank(string scenario, Dictionary<string, object?> request)
    {
        _ = scenario;
        using var bank = new RecordingBankHandler();
        using var factory = CreateFactory(bank);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/Payments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("errors").EnumerateObject().Any());
        Assert.Equal(0, bank.Calls);
        var json = body.GetRawText();
        Assert.DoesNotContain("2222405343248877", json);
        Assert.DoesNotContain("987", body.GetProperty("errors").GetRawText());
    }

    [Theory]
    [InlineData("{", "application/json")]
    [InlineData("null", "application/json")]
    [InlineData("{}", "application/json")]
    public async Task given_an_invalid_json_body_when_submitted_then_rejects_without_calling_bank(string body, string contentType)
    {
        using var bank = new RecordingBankHandler();
        using var factory = CreateFactory(bank);
        using var client = factory.CreateClient();
        using var content = new StringContent(body, System.Text.Encoding.UTF8, contentType);

        using var response = await client.PostAsync("/api/Payments", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, bank.Calls);
    }

    internal static Dictionary<string, object?> ValidRequest() => new()
    {
        ["cardNumber"] = "2222405343248877",
        ["expiryMonth"] = 4,
        ["expiryYear"] = 2031,
        ["currency"] = "GBP",
        ["amount"] = 1050,
        ["cvv"] = "987"
    };

    internal static WebApplicationFactory<PaymentsController> CreateFactory(RecordingBankHandler bank)
    {
        return new WebApplicationFactory<PaymentsController>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedTimeProvider());
                services.AddHttpClient("Bank").ConfigurePrimaryHttpMessageHandler(() => bank);
            }));
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2030, 6, 15, 12, 0, 0, TimeSpan.Zero);
    }
}

internal sealed class RecordingBankHandler : HttpMessageHandler
{
    public int Calls { get; private set; }
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } =
        (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { authorized = true, authorization_code = "bank-auth" })
        });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        return Respond(request, cancellationToken);
    }
}