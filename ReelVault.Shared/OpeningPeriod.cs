namespace ReelVault.Shared;

// One open/close span from Google Places' regularOpeningHours.periods. OpenDay/CloseDay can differ
// when hours cross midnight (e.g. open Friday 8:30 PM, close Saturday 1:00 AM). Day numbering matches
// System.DayOfWeek (0 = Sunday .. 6 = Saturday), same as Places' own "day" field.
public class OpeningPeriod
{
    public DayOfWeek OpenDay { get; set; }
    public TimeOnly OpenTime { get; set; }
    public DayOfWeek CloseDay { get; set; }
    public TimeOnly CloseTime { get; set; }
}
