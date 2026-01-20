using ElectionForecaster.Core.Enums;

namespace ElectionForecaster.Core.Models;

public class Race
{
    public string Id { get; set; } = string.Empty;
    public string StateId { get; set; } = string.Empty;
    public RaceType Type { get; set; }
    public int? DistrictNumber { get; set; } // Only for House races
    public RaceRating Rating { get; set; }
    public List<Candidate> Candidates { get; set; } = new();
    public List<Forecast> Forecasts { get; set; } = new();
    public bool IsSpecialElection { get; set; }
    public int Year { get; set; } = 2024;
}
