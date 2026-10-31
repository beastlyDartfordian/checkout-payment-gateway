using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Models;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController(
    PaymentsRepository paymentsRepository,
    BankClient bank,
    ILogger<PaymentsController> logger) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(PostPaymentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<PostPaymentResponse>> ProcessPayment(
        PostPaymentRequest request, CancellationToken cancellationToken)
    {
        bool authorized;
        try
        {
            authorized = await bank.Authorize(request, cancellationToken);
        }
        catch (BankException exception)
        {
            logger.LogWarning("Bank request failed with HTTP {StatusCode}; trace {TraceId}",
                exception.StatusCode, HttpContext.TraceIdentifier);
            return Problem(statusCode: exception.StatusCode,
                title: exception.StatusCode == 504 ? "Bank request timed out." : "Bank request failed.",
                detail: "No confirmed payment outcome is available. Do not assume the payment was declined.");
        }

        var payment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            Status = authorized ? PaymentStatus.Authorized : PaymentStatus.Declined,
            CardNumberLastFour = request.CardNumber![^4..],
            ExpiryMonth = request.ExpiryMonth!.Value,
            ExpiryYear = request.ExpiryYear!.Value,
            Currency = request.Currency!,
            Amount = request.Amount!.Value
        };
        paymentsRepository.Add(payment);
        logger.LogInformation("Payment {PaymentId} completed with status {PaymentStatus}; trace {TraceId}",
            payment.Id, payment.Status, HttpContext.TraceIdentifier);
        return CreatedAtAction(nameof(GetPayment), new { id = payment.Id }, payment);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PostPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<PostPaymentResponse> GetPayment(Guid id)
    {
        var payment = paymentsRepository.Get(id);
        return payment is null ? NotFound() : Ok(payment);
    }
}