using CollegeEventRegistration.Core.Entities;

namespace CollegeEventRegistration.Web.Models.Home;

/// <summary>
/// Assigns locally hosted event photography to the homepage cards. Uploaded banners always win;
/// otherwise each card receives a distinct photo matched to its category, so the same image is
/// never repeated across the visible set.
/// </summary>
public static class EventImageCatalog
{
    public sealed record CampusPhoto(string Url, string Alt);

    private static readonly CampusPhoto Fallback =
        new("/images/events/event-neutral-fallback.svg", "Campus event");

    private static readonly CampusPhoto WorkshopElectronics =
        new("/images/events/workshop-electronics.jpg", "Students assembling an electronics prototype at a workshop bench");

    private static readonly CampusPhoto WorkshopRobotics =
        new("/images/events/workshop-robotics.jpg", "Students examining a small robotics prototype in a laboratory");

    private static readonly CampusPhoto HackathonTable =
        new("/images/events/hackathon-table.jpg", "Students working around laptops at a shared hackathon table");

    private static readonly CampusPhoto HackathonCoding =
        new("/images/events/hackathon-coding.jpg", "Students coding together at a long table during a campus hackathon");

    private static readonly CampusPhoto LectureHall =
        new("/images/events/lecture-hall.jpg", "Students taking notes during a lecture in an auditorium");

    private static readonly CampusPhoto SymposiumPanel =
        new("/images/events/symposium-panel.jpg", "Speakers seated at a table during a symposium session");

    private static readonly CampusPhoto CulturalNight =
        new("/images/events/cultural-night.jpg", "Student ensemble performing on an outdoor stage at dusk");

    private static readonly CampusPhoto CulturalRehearsal =
        new("/images/events/cultural-rehearsal.jpg", "Student musicians rehearsing in a university practice room");

    /// <summary>Ordered pools per category; the first unused photo is taken for each event.</summary>
    private static readonly (string[] Keywords, CampusPhoto[] Photos)[] CategoryPools =
    {
        (new[] { "workshop", "technical" }, new[] { WorkshopElectronics, WorkshopRobotics }),
        (new[] { "hackathon", "contest" }, new[] { HackathonTable, HackathonCoding }),
        (new[] { "lecture", "seminar" }, new[] { LectureHall, HackathonCoding }),
        (new[] { "symposium", "conference" }, new[] { SymposiumPanel, LectureHall }),
        (new[] { "cultural", "arts" }, new[] { CulturalNight, CulturalRehearsal })
    };

    private static readonly CampusPhoto[] OverflowPool =
    {
        WorkshopElectronics, HackathonTable, LectureHall, SymposiumPanel, WorkshopRobotics, CulturalNight, HackathonCoding, CulturalRehearsal
    };

    /// <summary>
    /// Returns one photo per event, keyed by event id. Uploaded banners take priority, and no local
    /// photo is used twice while an unused alternative exists.
    /// </summary>
    public static IReadOnlyDictionary<int, CampusPhoto> Assign(IReadOnlyList<Event> events)
    {
        var assigned = new Dictionary<int, CampusPhoto>(events.Count);
        var usedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var evt in events)
        {
            // Only app-relative uploads are used: the site's Content-Security-Policy allows
            // same-origin images, so an externally hosted banner would be blocked outright.
            if (IsLocalUpload(evt.BannerImageUrl))
            {
                assigned[evt.Id] = new CampusPhoto(evt.BannerImageUrl!, $"{evt.Title} banner");
                usedUrls.Add(evt.BannerImageUrl!);
                continue;
            }

            var photo = TakeUnused(Candidates(evt), usedUrls);

            if (photo is null)
            {
                assigned[evt.Id] = Fallback;
                continue;
            }

            usedUrls.Add(photo.Url);
            assigned[evt.Id] = photo;
        }

        return assigned;
    }

    private static bool IsLocalUpload(string? bannerUrl)
    {
        return !string.IsNullOrWhiteSpace(bannerUrl)
               && bannerUrl.StartsWith('/')
               && !bannerUrl.StartsWith("//", StringComparison.Ordinal);
    }

    /// <summary>
    /// Ordered candidate photos for an event: the title is the most specific signal (an event called
    /// "CodeSprint" should not receive a lecture-hall photo just because its category is broad), then the
    /// stored category, then the shared pool as a last resort so no card is ever repeated.
    /// </summary>
    private static IEnumerable<CampusPhoto> Candidates(Event evt)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in new[]
                 {
                     evt.Title,
                     $"{evt.EventCategory?.Slug} {evt.EventCategory?.Name}"
                 })
        {
            foreach (var photo in MatchPool(source))
            {
                if (seen.Add(photo.Url))
                {
                    yield return photo;
                }
            }
        }

        foreach (var photo in OverflowPool)
        {
            if (seen.Add(photo.Url))
            {
                yield return photo;
            }
        }
    }

    private static IEnumerable<CampusPhoto> MatchPool(string? haystack)
    {
        if (string.IsNullOrWhiteSpace(haystack))
        {
            return Array.Empty<CampusPhoto>();
        }

        return CategoryPools
            .Where(pool => pool.Keywords.Any(keyword => haystack.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .SelectMany(pool => pool.Photos)
            .Distinct();
    }

    private static CampusPhoto? TakeUnused(IEnumerable<CampusPhoto> candidates, HashSet<string> usedUrls)
    {
        return candidates.FirstOrDefault(photo => !usedUrls.Contains(photo.Url));
    }
}
