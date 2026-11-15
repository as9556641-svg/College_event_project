namespace CollegeEventRegistration.Core.Enums;

public enum EventType
{
    Free = 1,
    Paid = 2
}

public enum EventMode
{
    Offline = 1,
    Online = 2,
    Hybrid = 3
}

public enum EventStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Rejected = 4,
    Published = 5,
    Ongoing = 6,
    Completed = 7,
    Cancelled = 8
}

public enum RegistrationStatus
{
    Pending = 1,
    Confirmed = 2,
    Waitlisted = 3,
    Cancelled = 4
}

public enum PaymentStatus
{
    NotApplicable = 1,
    Pending = 2,
    Success = 3,
    Failed = 4,
    Refunded = 5
}

public enum AttendanceStatus
{
    NotAttended = 1,
    CheckedIn = 2,
    Excused = 3
}

public enum CheckInMethod
{
    QrCode = 1,
    Manual = 2
}

public enum NotificationType
{
    General = 1,
    RegistrationConfirmation = 2,
    EventReminder = 3,
    EventUpdate = 4,
    AttendanceConfirmed = 5,
    CertificateAvailable = 6
}

public enum CertificateStatus
{
    Active = 1,
    Revoked = 2
}

