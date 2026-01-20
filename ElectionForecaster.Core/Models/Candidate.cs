using ElectionForecaster.Core.Enums;

namespace ElectionForecaster.Core.Models;

public class Candidate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Party Party { get; set; }
    public bool IsIncumbent { get; set; }
}
