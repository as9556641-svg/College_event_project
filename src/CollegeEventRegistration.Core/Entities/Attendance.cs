using CollegeEventRegistration.Core.Common;
using CollegeEventRegistration.Core.Enums;

namespace CollegeEventRegistration.Core.Entities;

/// <summary>
/// Physical or virtual check-in attendance verification record.
/// </summary>
public class Attendance : BaseEntity
{
    public int RegistrationId { get; set; }
    public EventRegistration Registration { get; set; } = null!;

    public DateTime CheckedInAt { get; set; } = DateTime.UtcNow;
    public string CheckedInByUserId { get; set; } = string.Empty;
    public ApplicationUser CheckedInByUser { get; set; } = null!;

    public AttendanceStatus Status { get; set; } = AttendanceStatus.CheckedIn;
    public CheckInMethod CheckInMethod { get; set; } = CheckInMethod.QrCode;
    public string? Remarks { get; set; }
}

