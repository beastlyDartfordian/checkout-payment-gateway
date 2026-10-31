using System.Net;
using System.Text.Json;

using PaymentGateway.Api.Models.Requests;

namespace PaymentGateway.Api.Services;

public sealed class BankClient(IHttpClientFactory clients)
{
    public async Task<bool> Authorize(PostPaymentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            using var client = clients.CreateClient("Bank");
            using var response = await client.PostAsJsonAsync("payments", new
            {
                card_number = request.CardNumber,
                expiry_date = FormattableString.Invariant($"{request.ExpiryMonth:00}/{request.ExpiryYear:0000}"),
                currency = request.Currency,
                amount = request.Amount,
                cvv = request.Cvv
            }, cancellationToken);

            if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                throw new BankException(503);
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new BankException(502);
            }

            var result = await response.Content.ReadFromJsonAsync<BankResponse>(cancellationToken);
            return result?.Authorized ?? throw new BankException(502);
        }
        catch (HttpRequestException)
        {
            throw new BankException(503);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new BankException(504);
        }
        catch (JsonException)
        {
            throw new BankException(502);
        }
        catch (NotSupportedException)
        {
            throw new BankException(502);
        }
    }

    private sealed record BankResponse(bool? Authorized);
}

public sealed class BankException(int statusCode) : Exception("Bank did not provide a usable payment outcome.")
{
    public int StatusCode { get; } = statusCode;
}