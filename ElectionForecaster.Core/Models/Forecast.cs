namespace ElectionForecaster.Core.Models;

public class Forecast
{
    public string CandidateId { get; set; } = string.Empty;
    public string CandidateName { get; set; } = string.Empty;
    public double WinProbability { get; set; } // 0.0 to 1.0
    public double ProjectedVoteShare { get; set; } // 0.0 to 1.0
}
