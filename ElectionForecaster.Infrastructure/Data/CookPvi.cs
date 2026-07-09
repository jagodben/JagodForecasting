namespace ElectionForecaster.Infrastructure.Data;

/// <summary>
/// Single source of truth for the Cook Partisan Voter Index, exposed consistently as a
/// <b>Democratic</b> lean in margin points (positive = D+X, negative = R+X). State values are the
/// 2024 Cook PVI (2020+2024 presidential results). District values wrap
/// <see cref="DistrictElectionData.DistrictPVI"/>, which stores the opposite (R-positive)
/// convention — this class flips the sign so callers never juggle it.
/// </summary>
public static class CookPvi
{
    /// <summary>State PVI as a Democratic lean (positive = Dem).</summary>
    public static readonly IReadOnlyDictionary<string, double> StateLean = new Dictionary<string, double>
    {
        { "AL", -15 }, { "AK", -9 }, { "AZ", -2 }, { "AR", -16 },
        { "CA", 14 }, { "CO", 6 }, { "CT", 8 }, { "DE", 7 },
        { "FL", -6 }, { "GA", 0 }, { "HI", 15 }, { "ID", -19 },
        { "IL", 8 }, { "IN", -10 }, { "IA", -6 }, { "KS", -10 },
        { "KY", -16 }, { "LA", -13 }, { "ME", 3 }, { "MD", 14 },
        { "MA", 16 }, { "MI", 1 }, { "MN", 2 }, { "MS", -10 },
        { "MO", -10 }, { "MT", -11 }, { "NE", -12 }, { "NV", 0 },
        { "NH", 1 }, { "NJ", 7 }, { "NM", 5 }, { "NY", 10 },
        { "NC", -3 }, { "ND", -20 }, { "OH", -6 }, { "OK", -20 },
        { "OR", 6 }, { "PA", 0 }, { "RI", 10 }, { "SC", -8 },
        { "SD", -16 }, { "TN", -14 }, { "TX", -5 }, { "UT", -11 },
        { "VT", 16 }, { "VA", 4 }, { "WA", 8 }, { "WV", -23 },
        { "WI", 0 }, { "WY", -25 }
    };

    /// <summary>State PVI as a Democratic lean, or 0 if the state is unknown.</summary>
    public static double GetStateLean(string stateId)
        => StateLean.TryGetValue(stateId.ToUpperInvariant(), out var v) ? v : 0.0;

    /// <summary>
    /// District PVI as a Democratic lean (positive = Dem). Districts absent from the table fall
    /// back to their state's lean, so there's no fabricated per-district variation.
    /// </summary>
    public static double GetDistrictLean(string stateId, int districtNumber)
    {
        stateId = stateId.ToUpperInvariant();
        var key = $"{stateId}-{districtNumber:D2}";
        return DistrictElectionData.DistrictPVI.TryGetValue(key, out var rLean)
            ? -rLean               // table is R-positive; flip to D-positive
            : GetStateLean(stateId);
    }
}
