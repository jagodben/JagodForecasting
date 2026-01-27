using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Models;

namespace ElectionForecaster.Infrastructure.Data;

public static class MockDataProvider
{
    private static readonly Random _random = new(42); // Fixed seed for consistent data

    public static List<State> GetAllStates()
    {
        // 2026 Election Data
        // Senate: Class 2 senators (elected 2020, up for re-election 2026)
        // Governors: 36 states have gubernatorial elections in 2026
        var stateData = new List<(string Id, string Name, int ElectoralVotes, int Districts, RaceRating Rating, bool HasSenateRace, bool HasGovRace)>
        {
            ("AL", "Alabama", 9, 7, RaceRating.SolidRep, true, true),
            ("AK", "Alaska", 3, 1, RaceRating.LikelyRep, true, true),
            ("AZ", "Arizona", 11, 9, RaceRating.TiltDem, false, true),
            ("AR", "Arkansas", 6, 4, RaceRating.SolidRep, true, true),
            ("CA", "California", 54, 52, RaceRating.SolidDem, false, true),
            ("CO", "Colorado", 10, 8, RaceRating.LikelyDem, true, true),
            ("CT", "Connecticut", 7, 5, RaceRating.SolidDem, false, true),
            ("DE", "Delaware", 3, 1, RaceRating.SolidDem, true, false),
            ("FL", "Florida", 30, 28, RaceRating.LeanRep, false, true),
            ("GA", "Georgia", 16, 14, RaceRating.TiltDem, true, true),
            ("HI", "Hawaii", 4, 2, RaceRating.SolidDem, false, true),
            ("ID", "Idaho", 4, 2, RaceRating.SolidRep, true, true),
            ("IL", "Illinois", 19, 17, RaceRating.SolidDem, true, true),
            ("IN", "Indiana", 11, 9, RaceRating.SolidRep, false, false),
            ("IA", "Iowa", 6, 4, RaceRating.LikelyRep, true, true),
            ("KS", "Kansas", 6, 4, RaceRating.SolidRep, true, true),
            ("KY", "Kentucky", 8, 6, RaceRating.SolidRep, true, false),
            ("LA", "Louisiana", 8, 6, RaceRating.SolidRep, true, false),
            ("ME", "Maine", 4, 2, RaceRating.LeanDem, true, true),
            ("MD", "Maryland", 10, 8, RaceRating.SolidDem, false, true),
            ("MA", "Massachusetts", 11, 9, RaceRating.SolidDem, true, true),
            ("MI", "Michigan", 15, 13, RaceRating.TiltDem, true, true),
            ("MN", "Minnesota", 10, 8, RaceRating.LeanDem, true, true),
            ("MS", "Mississippi", 6, 4, RaceRating.SolidRep, true, false),
            ("MO", "Missouri", 10, 8, RaceRating.SolidRep, false, false),
            ("MT", "Montana", 4, 2, RaceRating.LikelyRep, true, false),
            ("NE", "Nebraska", 5, 3, RaceRating.SolidRep, true, true),
            ("NV", "Nevada", 6, 4, RaceRating.TiltDem, false, true),
            ("NH", "New Hampshire", 4, 2, RaceRating.LeanDem, true, true),
            ("NJ", "New Jersey", 14, 12, RaceRating.LikelyDem, true, false),
            ("NM", "New Mexico", 5, 3, RaceRating.LikelyDem, true, true),
            ("NY", "New York", 28, 26, RaceRating.SolidDem, false, true),
            ("NC", "North Carolina", 16, 14, RaceRating.TiltDem, true, false),
            ("ND", "North Dakota", 3, 1, RaceRating.SolidRep, false, false),
            ("OH", "Ohio", 17, 15, RaceRating.LeanRep, false, true),
            ("OK", "Oklahoma", 7, 5, RaceRating.SolidRep, true, true),
            ("OR", "Oregon", 8, 6, RaceRating.LikelyDem, true, true),
            ("PA", "Pennsylvania", 19, 17, RaceRating.TiltDem, false, true),
            ("RI", "Rhode Island", 4, 2, RaceRating.SolidDem, true, true),
            ("SC", "South Carolina", 9, 7, RaceRating.SolidRep, true, true),
            ("SD", "South Dakota", 3, 1, RaceRating.SolidRep, true, true),
            ("TN", "Tennessee", 11, 9, RaceRating.SolidRep, true, true),
            ("TX", "Texas", 40, 38, RaceRating.LikelyRep, true, true),
            ("UT", "Utah", 6, 4, RaceRating.SolidRep, false, false),
            ("VT", "Vermont", 3, 1, RaceRating.SolidDem, false, true),
            ("VA", "Virginia", 13, 11, RaceRating.LeanDem, true, false),
            ("WA", "Washington", 12, 10, RaceRating.SolidDem, false, false),
            ("WV", "West Virginia", 4, 2, RaceRating.SolidRep, true, false),
            ("WI", "Wisconsin", 10, 8, RaceRating.TiltDem, false, true),
            ("WY", "Wyoming", 3, 1, RaceRating.SolidRep, true, true)
        };

        var states = stateData.Select(s => CreateState(s.Id, s.Name, s.ElectoralVotes, s.Districts, s.Rating, s.HasSenateRace, s.HasGovRace)).ToList();
        return states;
    }

    private static State CreateState(string id, string name, int electoralVotes, int districts, RaceRating rating, bool hasSenateRace, bool hasGovRace)
    {
        var state = new State
        {
            Id = id,
            Name = name,
            ElectoralVotes = electoralVotes,
            CongressionalDistricts = districts,
            OverallRating = rating,
            Races = new List<Race>(),
            Districts = new List<District>()
        };

        // Add Senate race if applicable
        if (hasSenateRace)
        {
            var senateRace = CreateSenateRace(id, rating);
            state.Races.Add(senateRace);
        }

        // Add Governor race if applicable
        if (hasGovRace)
        {
            var govRace = CreateGovernorRace(id, rating);
            state.Races.Add(govRace);
        }

        // Add House races for each district
        for (int i = 1; i <= districts; i++)
        {
            var districtRating = GetDistrictRating(rating, i, districts);
            var district = new District
            {
                Id = $"{id}-{i:D2}",
                StateId = id,
                Number = i,
                Rating = districtRating
            };

            var houseRace = CreateHouseRace(id, i, districtRating);
            district.HouseRace = houseRace;
            state.Races.Add(houseRace);
            state.Districts.Add(district);
        }

        return state;
    }

    private static Race CreateSenateRace(string stateId, RaceRating stateRating)
    {
        const string demName = "Democratic Nominee";
        const string repName = "Republican Nominee";
        var demIncumbent = IsDemocratIncumbentSenate(stateId);
        var (demProb, repProb) = GetProbabilities(stateRating);

        return new Race
        {
            Id = $"{stateId}-SEN-2026",
            StateId = stateId,
            Type = RaceType.Senate,
            Rating = stateRating,
            Year = 2026,
            Candidates = new List<Candidate>
            {
                new() { Id = $"{stateId}-SEN-D", Name = demName, Party = Party.Democrat, IsIncumbent = demIncumbent },
                new() { Id = $"{stateId}-SEN-R", Name = repName, Party = Party.Republican, IsIncumbent = !demIncumbent }
            },
            Forecasts = new List<Forecast>
            {
                new() { CandidateId = $"{stateId}-SEN-D", CandidateName = demName, WinProbability = demProb, ProjectedVoteShare = 0.45 + (demProb - 0.5) * 0.2 },
                new() { CandidateId = $"{stateId}-SEN-R", CandidateName = repName, WinProbability = repProb, ProjectedVoteShare = 0.45 + (repProb - 0.5) * 0.2 }
            }
        };
    }

    private static Race CreateGovernorRace(string stateId, RaceRating stateRating)
    {
        const string demName = "Democratic Nominee";
        const string repName = "Republican Nominee";
        var demIncumbent = IsDemocratIncumbentGovernor(stateId);
        var (demProb, repProb) = GetProbabilities(stateRating);

        return new Race
        {
            Id = $"{stateId}-GOV-2026",
            StateId = stateId,
            Type = RaceType.Governor,
            Rating = stateRating,
            Year = 2026,
            Candidates = new List<Candidate>
            {
                new() { Id = $"{stateId}-GOV-D", Name = demName, Party = Party.Democrat, IsIncumbent = demIncumbent },
                new() { Id = $"{stateId}-GOV-R", Name = repName, Party = Party.Republican, IsIncumbent = !demIncumbent }
            },
            Forecasts = new List<Forecast>
            {
                new() { CandidateId = $"{stateId}-GOV-D", CandidateName = demName, WinProbability = demProb, ProjectedVoteShare = 0.45 + (demProb - 0.5) * 0.2 },
                new() { CandidateId = $"{stateId}-GOV-R", CandidateName = repName, WinProbability = repProb, ProjectedVoteShare = 0.45 + (repProb - 0.5) * 0.2 }
            }
        };
    }

    private static Race CreateHouseRace(string stateId, int districtNumber, RaceRating rating)
    {
        const string demName = "Democratic Nominee";
        const string repName = "Republican Nominee";
        var demIncumbent = rating <= RaceRating.LeanDem;
        var (demProb, repProb) = GetProbabilities(rating);

        return new Race
        {
            Id = $"{stateId}-{districtNumber:D2}-2026",
            StateId = stateId,
            Type = RaceType.House,
            DistrictNumber = districtNumber,
            Rating = rating,
            Year = 2026,
            Candidates = new List<Candidate>
            {
                new() { Id = $"{stateId}-{districtNumber:D2}-D", Name = demName, Party = Party.Democrat, IsIncumbent = demIncumbent },
                new() { Id = $"{stateId}-{districtNumber:D2}-R", Name = repName, Party = Party.Republican, IsIncumbent = !demIncumbent }
            },
            Forecasts = new List<Forecast>
            {
                new() { CandidateId = $"{stateId}-{districtNumber:D2}-D", CandidateName = demName, WinProbability = demProb, ProjectedVoteShare = 0.45 + (demProb - 0.5) * 0.2 },
                new() { CandidateId = $"{stateId}-{districtNumber:D2}-R", CandidateName = repName, WinProbability = repProb, ProjectedVoteShare = 0.45 + (repProb - 0.5) * 0.2 }
            }
        };
    }

    private static bool IsDemocratIncumbentSenate(string stateId)
    {
        // Class 2 Senate seats with Democratic incumbents (as of 2026)
        var demIncumbentStates = new HashSet<string>
        {
            "CO", // Hickenlooper
            "DE", // Coons
            "GA", // Ossoff
            "IL", // Durbin
            "MA", // Markey
            "MI", // Peters
            "MN", // Smith
            "NH", // Shaheen
            "NJ", // Booker
            "NM", // Lujan
            "OR", // Merkley
            "RI", // Reed
            "VA"  // Warner
        };
        return demIncumbentStates.Contains(stateId);
    }

    private static bool IsDemocratIncumbentGovernor(string stateId)
    {
        // States with Democratic governors up in 2026
        var demIncumbentStates = new HashSet<string>
        {
            "AZ", // Hobbs
            "CA", // Newsom (term-limited)
            "CO", // Polis (term-limited)
            "CT", // Lamont
            "HI", // Green
            "IL", // Pritzker
            "KS", // Kelly
            "KY", // Beshear (term-limited)
            "ME", // Mills
            "MD", // Moore
            "MA", // Healey
            "MI", // Whitmer (term-limited)
            "MN", // Walz
            "NV", // Lombardo is R
            "NM", // Lujan Grisham (term-limited)
            "NY", // Hochul
            "NC", // Cooper (term-limited) -> 2024 winner
            "OR", // Kotek
            "PA", // Shapiro
            "RI", // McKee
            "WI"  // Evers
        };
        return demIncumbentStates.Contains(stateId);
    }

    private static RaceRating GetDistrictRating(RaceRating stateRating, int districtNumber, int totalDistricts)
    {
        // Create variety in district ratings based on state rating
        var baseOffset = (districtNumber % 3) - 1; // -1, 0, or 1
        var ratingValue = (int)stateRating + baseOffset;
        ratingValue = Math.Max(0, Math.Min(6, ratingValue)); // Clamp to valid range
        return (RaceRating)ratingValue;
    }

    private static (double demProb, double repProb) GetProbabilities(RaceRating rating)
    {
        return rating switch
        {
            RaceRating.SolidDem => (0.95, 0.05),
            RaceRating.LikelyDem => (0.80, 0.20),
            RaceRating.LeanDem => (0.65, 0.35),
            RaceRating.TiltDem => (0.55, 0.45),
            RaceRating.TiltRep => (0.45, 0.55),
            RaceRating.LeanRep => (0.35, 0.65),
            RaceRating.LikelyRep => (0.20, 0.80),
            RaceRating.SolidRep => (0.05, 0.95),
            _ => (0.50, 0.50)
        };
    }

}
