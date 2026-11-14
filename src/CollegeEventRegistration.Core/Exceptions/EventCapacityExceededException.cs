namespace CollegeEventRegistration.Core.Exceptions;

/// <summary>
/// Thrown when an attendee attempts to register for an event that has reached maximum capacity.
/// </summary>
public class EventCapacityExceededException : InvalidOperationException
{
    public int EventId { get; }
    public int TotalCapacity { get; }

    public EventCapacityExceededException(int eventId, int totalCapacity)
        : base($"Event #{eventId} has reached its maximum capacity of {totalCapacity} attendees.")
    {
        EventId = eventId;
        TotalCapacity = totalCapacity;
    }
}
