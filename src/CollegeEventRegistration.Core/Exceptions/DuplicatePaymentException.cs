namespace CollegeEventRegistration.Core.Exceptions;

/// <summary>
/// Thrown when a payment attempt is submitted for a registration that already has a verified successful payment.
/// </summary>
public class DuplicatePaymentException : InvalidOperationException
{
    public int RegistrationId { get; }
    public string? ExistingTransactionId { get; }

    public DuplicatePaymentException(int registrationId, string? existingTransactionId = null)
        : base($"Registration #{registrationId} already has a successful verified payment (Transaction: {existingTransactionId ?? "Existing"}).")
    {
        RegistrationId = registrationId;
        ExistingTransactionId = existingTransactionId;
    }
}
