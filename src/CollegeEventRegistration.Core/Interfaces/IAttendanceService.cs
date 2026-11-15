using System.Threading;
using System.Threading.Tasks;
using CollegeEventRegistration.Core.Common;
using CollegeEventRegistration.Core.DTOs;

namespace CollegeEventRegistration.Core.Interfaces;

/// <summary>
/// Domain service interface for Phase 10: Attendance Management.
/// Enforces cryptographic verification, event-scoped authorization,
/// duplicate scan prevention, atomic persistence, and manual check-in auditing.
/// </summary>
public interface IAttendanceService
{
    /// <summary>
    /// Processes an attendance check-in request via QR code payload, ticket code, or manual roll entry.
    /// Strictly validates cryptographic signatures, event scoping, payment completeness, and duplicate prevention.
    /// </summary>
    Task<CheckInResultDto> CheckInAsync(
        CheckInRequestDto request,
        string staffUserId,
        string staffRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the check-in metrics summary for an event, validating staff authorization.
    /// </summary>
    Task<EventAttendanceSummaryDto> GetEventAttendanceSummaryAsync(
        int eventId,
        string staffUserId,
        string staffRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a paged list of attendance records with optional filtering, enforcing coordinator event scoping.
    /// </summary>
    Task<PagedResult<AttendanceRecordDto>> GetAttendanceRecordsAsync(
        AttendanceFilterCriteria criteria,
        string staffUserId,
        string staffRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves detailed attendance audit information for a specific record.
    /// </summary>
    Task<AttendanceRecordDto?> GetAttendanceDetailsAsync(
        int attendanceId,
        string staffUserId,
        string staffRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a specific registration has already been checked in.
    /// </summary>
    Task<bool> IsStudentCheckedInAsync(
        int registrationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates whether the specified staff user has authorization to manage attendance for the given event.
    /// Admins and SuperAdmins have campus-wide access; Coordinators must be assigned to the event.
    /// </summary>
    Task<bool> CanStaffManageEventAttendanceAsync(
        int eventId,
        string staffUserId,
        string staffRole,
        CancellationToken cancellationToken = default);
}
