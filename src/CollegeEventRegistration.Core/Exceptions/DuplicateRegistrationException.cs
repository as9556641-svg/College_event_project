namespace CollegeEventRegistration.Core.Exceptions;

/// <summary>
/// Thrown when an attendee attempts to register for an event they are already actively registered for.
/// </summary>
public class DuplicateRegistrationException : InvalidOperationException
{
    public int EventId { get; }
    public string UserId { get; }
    public string RegistrationNumber { get; }

    public DuplicateRegistrationException(int eventId, string userId, string registrationNumber)
        : base($"User '{userId}' is already registered for Event #{eventId} with Registration #{registrationNumber}.")
    {
        EventId = eventId;
        UserId = userId;
        RegistrationNumber = registrationNumber;
    }
}
