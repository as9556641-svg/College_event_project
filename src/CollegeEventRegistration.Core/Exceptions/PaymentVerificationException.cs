namespace CollegeEventRegistration.Core.Exceptions;

/// <summary>
/// Thrown when payment cryptographic signature, order association, or monetary amount verification fails.
/// </summary>
public class PaymentVerificationException : InvalidOperationException
{
    public string? OrderId { get; }
    public string? PaymentId { get; }

    public PaymentVerificationException(string message, string? orderId = null, string? paymentId = null)
        : base(message)
    {
        OrderId = orderId;
        PaymentId = paymentId;
    }
}
