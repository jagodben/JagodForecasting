namespace ElectionForecaster.Core.Models;

public class State
{
    public string Id { get; set; } = string.Empty; // e.g., "PA", "TX"
    public string Name { get; set; } = string.Empty;
    public int ElectoralVotes { get; set; }
    public int CongressionalDistricts { get; set; }
    public List<Race> Races { get; set; } = new();
    public List<District> Districts { get; set; } = new();
}
