using ElectionForecaster.Core.Enums;

namespace ElectionForecaster.Core.Models;

public class District
{
    public string Id { get; set; } = string.Empty; // e.g., "PA-01"
    public string StateId { get; set; } = string.Empty;
    public int Number { get; set; }
    public RaceRating Rating { get; set; }
    public Race? HouseRace { get; set; }
}
