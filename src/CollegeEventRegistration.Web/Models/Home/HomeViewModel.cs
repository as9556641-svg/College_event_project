using System.Globalization;
using CollegeEventRegistration.Core.Common;
using CollegeEventRegistration.Core.DTOs;
using CollegeEventRegistration.Core.Entities;
using CollegeEventRegistration.Core.Enums;
using CollegeEventRegistration.Core.Interfaces;

namespace CollegeEventRegistration.Web.Models.Home;

/// <summary>
/// Content for the public homepage: the featured event cards, the continuation list
/// and the event categories, all sourced from published institutional data.
/// </summary>
public sealed class HomeViewModel
{
    public IReadOnlyList<HomeEventCard> FeaturedEvents { get; init; } = Array.Empty<HomeEventCard>();

    public IReadOnlyList<HomeEventCard> MoreEvents { get; init; } = Array.Empty<HomeEventCard>();

    public IReadOnlyList<HomeCategoryCard> Categories { get; init; } = Array.Empty<HomeCategoryCard>();

    public bool HasEvents => FeaturedEvents.Count > 0 || MoreEvents.Count > 0;
}

/// <summary>
/// Display projection of a published event for the homepage. Carries only the fields
/// the cards render; routing stays in the Razor view.
/// </summary>
public sealed record HomeEventCard(
    int Id,
    string Title,
    string CategoryName,
    string DateLabel,
    string? DepartmentShortName,
    string? DepartmentName,
    string? Venue,
    bool IsOnline,
    string ModeLabel,
    string PriceLabel,
    int Capacity,
    string ImageUrl,
    string ImageAlt)
{
    public static HomeEventCard FromEntity(Event evt, EventImageCatalog.CampusPhoto photo)
    {
        ArgumentNullException.ThrowIfNull(evt);
        ArgumentNullException.ThrowIfNull(photo);

        var startInIndia = evt.EventStartDate.ToKolkataTime();
        var departmentName = evt.Department?.Name;
        var departmentShortName = !string.IsNullOrWhiteSpace(evt.Department?.Code)
            ? evt.Department!.Code
            : departmentName;

        return new HomeEventCard(
            evt.Id,
            evt.Title,
            evt.EventCategory?.Name ?? "Campus event",
            startInIndia.ToString("ddd dd MMM yyyy", CultureInfo.InvariantCulture),
            string.IsNullOrWhiteSpace(departmentShortName) ? null : departmentShortName,
            string.IsNullOrWhiteSpace(departmentName) ? null : departmentName,
            string.IsNullOrWhiteSpace(evt.Venue) ? null : evt.Venue,
            evt.EventMode == EventMode.Online,
            evt.EventMode.ToString(),
            FormatPrice(evt),
            evt.TotalCapacity,
            photo.Url,
            photo.Alt);
    }

    private static string FormatPrice(Event evt)
    {
        if (evt.EventType == EventType.Free || evt.RegistrationFee <= 0m)
        {
            return "Free";
        }

        return "₹" + evt.RegistrationFee.ToString("0.##", CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Display projection of an active event category, including the published event count.
/// </summary>
public sealed record HomeCategoryCard(int Id, string Name, string IconName, int PublishedCount)
{
    public static HomeCategoryCard FromDto(CategoryWithCountDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new HomeCategoryCard(dto.Id, dto.Name, IconLibrary.ResolveCategoryIcon(dto.Slug, dto.Name), dto.PublishedCount);
    }
}

/// <summary>
/// Builds the homepage view model from the published-only discovery service, keeping the
/// controller free of projection logic.
/// </summary>
public static class HomePageAssembler
{
    private const int FeaturedEventCount = 3;
    private const int TotalEventCount = 6;

    public static async Task<HomeViewModel> BuildAsync(IEventDiscoveryService discoveryService)
    {
        ArgumentNullException.ThrowIfNull(discoveryService);

        var eventsResult = await discoveryService.GetPublishedEventsAsync(new StudentEventQueryCriteria
        {
            PageNumber = 1,
            PageSize = TotalEventCount,
            SortBy = "upcoming"
        });

        var categories = await discoveryService.GetActiveCategoriesWithCountAsync();

        var events = eventsResult.Items.ToList();
        var photos = EventImageCatalog.Assign(events);
        var cards = events.Select(evt => HomeEventCard.FromEntity(evt, photos[evt.Id])).ToList();

        return new HomeViewModel
        {
            FeaturedEvents = cards.Take(FeaturedEventCount).ToList(),
            MoreEvents = cards.Skip(FeaturedEventCount).ToList(),
            Categories = categories.Select(HomeCategoryCard.FromDto).ToList()
        };
    }
}
