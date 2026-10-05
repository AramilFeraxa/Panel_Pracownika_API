using System.Globalization;
using PanelPracownika.Models;

namespace PanelPracownika.Services
{
    public static class WorkPeriodValidation
    {
        public static bool TryParse(string? value, out TimeSpan time)
        {
            return TimeSpan.TryParseExact(value, new[] { @"hh\:mm", @"hh\:mm\:ss" },
                CultureInfo.InvariantCulture, out time)
                && time >= TimeSpan.Zero && time < TimeSpan.FromDays(1) && time.Seconds == 0;
        }

        public static string? Validate(IReadOnlyList<WorkPeriodDto>? periods, IEnumerable<WorkTime> existing)
        {
            if (periods == null || periods.Count == 0)
                return "Dodaj co najmniej jeden okres pracy.";

            var ranges = new List<(TimeSpan Start, TimeSpan End)>();
            foreach (var period in periods)
            {
                if (period == null || !TryParse(period.StartTime, out var start) || !TryParse(period.EndTime, out var end))
                    return "Nieprawidłowy format godzin. Użyj HH:mm.";
                if (end <= start)
                    return "Godzina zakończenia musi być późniejsza niż godzina rozpoczęcia.";
                ranges.Add((start, end));
            }

            var saved = existing.Where(entry => entry.Total > 0)
                .Select(entry => (Start: TimeSpan.Parse(entry.StartTime, CultureInfo.InvariantCulture),
                    End: TimeSpan.Parse(entry.EndTime, CultureInfo.InvariantCulture))).ToList();
            for (var i = 0; i < ranges.Count; i++)
            {
                var range = ranges[i];
                if (ranges.Skip(i + 1).Concat(saved).Any(other => range.Start < other.End && other.Start < range.End))
                    return "Okresy pracy nie mogą się nakładać. Sprawdź także zapisane godziny tego dnia.";
            }
            return null;
        }
    }
}
