using System.Collections.Concurrent;

using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public sealed class PaymentsRepository
{
    private readonly ConcurrentDictionary<Guid, PostPaymentResponse> _payments = new();

    public IEnumerable<PostPaymentResponse> Payments => _payments.Values;

    public void Add(PostPaymentResponse payment)
    {
        if (!_payments.TryAdd(payment.Id, payment))
        {
            throw new InvalidOperationException("Payment ID already exists.");
        }
    }

    public PostPaymentResponse? Get(Guid id) => _payments.GetValueOrDefault(id);
}