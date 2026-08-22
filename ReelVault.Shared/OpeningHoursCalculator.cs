namespace ReelVault.Shared;

// Pure "is it open right now" computation from stored structured periods - deliberately takes the
// current time as a parameter rather than reading the clock itself, so it's testable and so the
// answer is always freshly computed at render time (never a stale snapshot from whenever the item
// was enriched).
public static class OpeningHoursCalculator
{
    // Null means "we don't know" (no hours data) - never guess. True/false is a real answer.
    public static bool? IsOpenNow(List<OpeningPeriod>? periods, DateTime now)
    {
        if (periods is null || periods.Count == 0)
        {
            return null;
        }

        var nowDay = now.DayOfWeek;
        var nowTime = TimeOnly.FromDateTime(now);

        foreach (var period in periods)
        {
            if (period.OpenDay == period.CloseDay)
            {
                if (nowDay == period.OpenDay && nowTime >= period.OpenTime && nowTime < period.CloseTime)
                {
                    return true;
                }
            }
            else
            {
                // Spans midnight: "open" covers the tail end of OpenDay and the start of CloseDay.
                if (nowDay == period.OpenDay && nowTime >= period.OpenTime)
                {
                    return true;
                }

                if (nowDay == period.CloseDay && nowTime < period.CloseTime)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
