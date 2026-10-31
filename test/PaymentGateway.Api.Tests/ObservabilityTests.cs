using System.Net;
using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PaymentGateway.Api.Tests;

public class ObservabilityTests
{
    [Fact]
    public async Task OpenApiDescribesActualResponsesAndCompletedStatuses()
    {
        using var bank = new RecordingBankHandler();
        using var factory = PaymentValidationTests.CreateFactory(bank);
        using var client = factory.CreateClient();

        var spec = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/swagger/v1/swagger.json");

        var paths = spec.GetProperty("paths");
        var postResponses = paths.GetProperty("/api/Payments").GetProperty("post").GetProperty("responses");
        foreach (var code in new[] { "201", "400", "502", "503", "504" })
        {
            Assert.True(postResponses.TryGetProperty(code, out _), $"POST should document {code}");
        }
        var getResponses = paths.GetProperty("/api/Payments/{id}").GetProperty("get").GetProperty("responses");
        Assert.True(getResponses.TryGetProperty("200", out _));
        Assert.True(getResponses.TryGetProperty("404", out _));
        var statuses = spec.GetProperty("components").GetProperty("schemas")
            .GetProperty("PaymentStatus").GetProperty("enum").EnumerateArray().Select(item => item.GetString());
        Assert.Equal(new[] { "Authorized", "Declined" }, statuses);
    }

    [Fact]
    public async Task HealthEndpointReportsProcessHealthWithoutCallingBank()
    {
        using var bank = new RecordingBankHandler();
        using var factory = PaymentValidationTests.CreateFactory(bank);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, bank.Calls);
    }

    [Fact]
    public async Task LogsOutcomeRejectionAndFailureWithoutCardData()
    {
        using var bank = new RecordingBankHandler();
        using var logs = new CapturedLogs();
        using var factory = PaymentValidationTests.CreateFactory(bank).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logs)));
        using var client = factory.CreateClient();
        var request = PaymentValidationTests.ValidRequest();

        using var success = await client.PostAsJsonAsync("/api/Payments", request);
        Assert.Equal(HttpStatusCode.Created, success.StatusCode);
        var invalid = PaymentValidationTests.ValidRequest();
        invalid["currency"] = "JPY";
        using var rejected = await client.PostAsJsonAsync("/api/Payments", invalid);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        bank.Respond = (_, _) => throw new HttpRequestException("upstream-secret");
        using var failure = await client.PostAsJsonAsync("/api/Payments", request);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failure.StatusCode);

        var messages = string.Join("\n", logs.Messages);
        Assert.Contains("Authorized", messages);
        Assert.Contains("Payment request rejected", messages);
        Assert.Contains("Bank request failed", messages);
        Assert.Contains("trace", messages, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("2222405343248877", messages);
        Assert.DoesNotContain("987", logs.Values);
        Assert.DoesNotContain("upstream-secret", messages);
        Assert.DoesNotContain("cvv", messages, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        public System.Collections.Concurrent.ConcurrentQueue<string> Messages { get; } = new();
        public System.Collections.Concurrent.ConcurrentQueue<string> Values { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CapturedLogger(Messages, Values);
        public void Dispose() { }

        private sealed class CapturedLogger(System.Collections.Concurrent.ConcurrentQueue<string> messages,
            System.Collections.Concurrent.ConcurrentQueue<string> values) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                Exception? exception, Func<TState, Exception?, string> formatter)
            {
                messages.Enqueue(formatter(state, exception));
                if (state is IEnumerable<KeyValuePair<string, object?>> fields)
                {
                    foreach (var field in fields)
                    {
                        values.Enqueue(field.Value?.ToString() ?? string.Empty);
                    }
                }
            }
        }
    }
}