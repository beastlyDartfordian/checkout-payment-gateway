using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

public class PaymentsControllerTests
{
    [Theory]
    [InlineData(PaymentStatus.Authorized)]
    [InlineData(PaymentStatus.Declined)]
    public async Task RetrievesSavedPaymentDetails(PaymentStatus status)
    {
        // Arrange
        var payment = CreatePayment(status);
        using var factory = CreateFactory(payment);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync($"/api/Payments/{payment.Id}");

        // Assert against JSON rather than deserializing into the API's response model.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(payment.Id, body.GetProperty("id").GetGuid());
        Assert.Equal(status.ToString(), body.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, body.GetProperty("cardNumberLastFour").ValueKind);
        Assert.Equal("8877", body.GetProperty("cardNumberLastFour").GetString());
        Assert.Equal(4, body.GetProperty("expiryMonth").GetInt32());
        Assert.Equal(2030, body.GetProperty("expiryYear").GetInt32());
        Assert.Equal("GBP", body.GetProperty("currency").GetString());
        Assert.Equal(1050, body.GetProperty("amount").GetInt32());
    }

    [Fact]
    public async Task Returns404IfPaymentNotFound()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/Payments/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReturnsOnlySafePaymentFields()
    {
        var payment = CreatePayment(PaymentStatus.Authorized);
        using var factory = CreateFactory(payment);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/Payments/{payment.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var expectedFields = new[]
        {
            "amount", "cardNumberLastFour", "currency", "expiryMonth", "expiryYear", "id", "status"
        };
        Assert.Equal(expectedFields, body.EnumerateObject().Select(property => property.Name).OrderBy(name => name));
    }

    [Fact]
    public async Task ReturnsLastFourDigitsWithLeadingZeros()
    {
        var payment = CreatePayment(PaymentStatus.Authorized);
        payment.CardNumberLastFour = "0007";
        using var factory = CreateFactory(payment);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/api/Payments/{payment.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.String, body.GetProperty("cardNumberLastFour").ValueKind);
        Assert.Equal("0007", body.GetProperty("cardNumberLastFour").GetString());
    }

    private static PostPaymentResponse CreatePayment(PaymentStatus status)
    {
        return new PostPaymentResponse
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Status = status,
            CardNumberLastFour = "8877",
            ExpiryMonth = 4,
            ExpiryYear = 2030,
            Currency = "GBP",
            Amount = 1050
        };
    }

    private static WebApplicationFactory<PaymentsController> CreateFactory(PostPaymentResponse? payment = null)
    {
        var repository = new PaymentsRepository();
        if (payment is not null)
        {
            repository.Add(payment);
        }

        return new WebApplicationFactory<PaymentsController>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<PaymentsRepository>();
                services.AddSingleton(repository);
            }));
    }
}