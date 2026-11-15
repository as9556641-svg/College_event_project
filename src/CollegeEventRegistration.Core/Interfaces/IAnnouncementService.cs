using CollegeEventRegistration.Core.Common;
using CollegeEventRegistration.Core.DTOs;

namespace CollegeEventRegistration.Core.Interfaces;

/// <summary>
/// Domain service for targeted campus and event announcements.
/// </summary>
public interface IAnnouncementService
{
    /// <summary>
    /// Retrieves active announcements targeted to the student (Department, Role=Student, Enrolled Events, or All).
    /// </summary>
    Task<PagedResult<AnnouncementDto>> GetActiveAnnouncementsForStudentAsync(
        string studentUserId,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves announcements managed by or visible to a coordinator (assigned events and campus-wide).
    /// </summary>
    Task<PagedResult<AnnouncementDto>> GetAnnouncementsForCoordinatorAsync(
        string coordinatorUserId,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all announcements across campus with administrative filters.
    /// </summary>
    Task<PagedResult<AnnouncementDto>> GetAllAnnouncementsAdminAsync(
        AnnouncementFilterDto filter,
        int page = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);

    Task<AnnouncementDto?> GetAnnouncementDetailsAsync(
        int id,
        string? requestingUserId = null,
        CancellationToken cancellationToken = default);

    Task<AnnouncementResultDto> CreateAnnouncementAsync(
        CreateAnnouncementDto dto,
        string authorUserId,
        bool isStaff,
        CancellationToken cancellationToken = default);

    Task<AnnouncementResultDto> UpdateAnnouncementAsync(
        int id,
        EditAnnouncementDto dto,
        string modifyingUserId,
        bool isStaff,
        CancellationToken cancellationToken = default);

    Task<bool> PublishAnnouncementAsync(
        int id,
        string modifyingUserId,
        bool isStaff,
        CancellationToken cancellationToken = default);

    Task<bool> ArchiveAnnouncementAsync(
        int id,
        string modifyingUserId,
        bool isStaff,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAnnouncementAsync(
        int id,
        string modifyingUserId,
        bool isStaff,
        CancellationToken cancellationToken = default);
}
