using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Models;

namespace ElectionForecaster.Infrastructure.Data;

public static class MockDataProvider
{
    private static readonly Random _random = new(42); // Fixed seed for consistent data

    public static List<State> GetAllStates()
    {
        var stateData = new List<(string Id, string Name, int ElectoralVotes, int Districts, RaceRating Rating, bool HasSenateRace, bool HasGovRace)>
        {
            ("AL", "Alabama", 9, 7, RaceRating.SolidRep, true, false),
            ("AK", "Alaska", 3, 1, RaceRating.LikelyRep, false, false),
            ("AZ", "Arizona", 11, 9, RaceRating.Tossup, true, false),
            ("AR", "Arkansas", 6, 4, RaceRating.SolidRep, false, false),
            ("CA", "California", 54, 52, RaceRating.SolidDem, true, false),
            ("CO", "Colorado", 10, 8, RaceRating.LikelyDem, false, false),
            ("CT", "Connecticut", 7, 5, RaceRating.SolidDem, true, false),
            ("DE", "Delaware", 3, 1, RaceRating.SolidDem, true, true),
            ("FL", "Florida", 30, 28, RaceRating.LeanRep, true, false),
            ("GA", "Georgia", 16, 14, RaceRating.Tossup, false, false),
            ("HI", "Hawaii", 4, 2, RaceRating.SolidDem, true, false),
            ("ID", "Idaho", 4, 2, RaceRating.SolidRep, false, false),
            ("IL", "Illinois", 19, 17, RaceRating.SolidDem, false, false),
            ("IN", "Indiana", 11, 9, RaceRating.SolidRep, true, true),
            ("IA", "Iowa", 6, 4, RaceRating.LikelyRep, false, false),
            ("KS", "Kansas", 6, 4, RaceRating.SolidRep, false, false),
            ("KY", "Kentucky", 8, 6, RaceRating.SolidRep, false, false),
            ("LA", "Louisiana", 8, 6, RaceRating.SolidRep, false, false),
            ("ME", "Maine", 4, 2, RaceRating.LeanDem, true, false),
            ("MD", "Maryland", 10, 8, RaceRating.SolidDem, true, false),
            ("MA", "Massachusetts", 11, 9, RaceRating.SolidDem, true, false),
            ("MI", "Michigan", 15, 13, RaceRating.Tossup, true, false),
            ("MN", "Minnesota", 10, 8, RaceRating.LeanDem, true, false),
            ("MS", "Mississippi", 6, 4, RaceRating.SolidRep, true, true),
            ("MO", "Missouri", 10, 8, RaceRating.SolidRep, true, true),
            ("MT", "Montana", 4, 2, RaceRating.LeanRep, true, true),
            ("NE", "Nebraska", 5, 3, RaceRating.SolidRep, true, false),
            ("NV", "Nevada", 6, 4, RaceRating.Tossup, true, false),
            ("NH", "New Hampshire", 4, 2, RaceRating.LeanDem, false, true),
            ("NJ", "New Jersey", 14, 12, RaceRating.LikelyDem, true, false),
            ("NM", "New Mexico", 5, 3, RaceRating.LikelyDem, true, false),
            ("NY", "New York", 28, 26, RaceRating.SolidDem, false, false),
            ("NC", "North Carolina", 16, 14, RaceRating.Tossup, false, true),
            ("ND", "North Dakota", 3, 1, RaceRating.SolidRep, true, true),
            ("OH", "Ohio", 17, 15, RaceRating.LeanRep, true, false),
            ("OK", "Oklahoma", 7, 5, RaceRating.SolidRep, false, false),
            ("OR", "Oregon", 8, 6, RaceRating.LikelyDem, false, false),
            ("PA", "Pennsylvania", 19, 17, RaceRating.Tossup, true, false),
            ("RI", "Rhode Island", 4, 2, RaceRating.SolidDem, true, false),
            ("SC", "South Carolina", 9, 7, RaceRating.SolidRep, false, false),
            ("SD", "South Dakota", 3, 1, RaceRating.SolidRep, false, false),
            ("TN", "Tennessee", 11, 9, RaceRating.SolidRep, true, false),
            ("TX", "Texas", 40, 38, RaceRating.LikelyRep, true, false),
            ("UT", "Utah", 6, 4, RaceRating.SolidRep, true, true),
            ("VT", "Vermont", 3, 1, RaceRating.SolidDem, true, true),
            ("VA", "Virginia", 13, 11, RaceRating.LeanDem, false, false),
            ("WA", "Washington", 12, 10, RaceRating.SolidDem, false, true),
            ("WV", "West Virginia", 4, 2, RaceRating.SolidRep, true, true),
            ("WI", "Wisconsin", 10, 8, RaceRating.Tossup, true, false),
            ("WY", "Wyoming", 3, 1, RaceRating.SolidRep, true, false)
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
        var (demName, repName) = GetCandidateNames(stateId, RaceType.Senate);
        var demIncumbent = stateRating <= RaceRating.LeanDem;
        var (demProb, repProb) = GetProbabilities(stateRating);

        return new Race
        {
            Id = $"{stateId}-SEN-2024",
            StateId = stateId,
            Type = RaceType.Senate,
            Rating = stateRating,
            Year = 2024,
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
        var (demName, repName) = GetCandidateNames(stateId, RaceType.Governor);
        var demIncumbent = stateRating <= RaceRating.LeanDem;
        var (demProb, repProb) = GetProbabilities(stateRating);

        return new Race
        {
            Id = $"{stateId}-GOV-2024",
            StateId = stateId,
            Type = RaceType.Governor,
            Rating = stateRating,
            Year = 2024,
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
        var (demName, repName) = GetDistrictCandidateNames(stateId, districtNumber);
        var demIncumbent = rating <= RaceRating.LeanDem;
        var (demProb, repProb) = GetProbabilities(rating);

        return new Race
        {
            Id = $"{stateId}-{districtNumber:D2}-2024",
            StateId = stateId,
            Type = RaceType.House,
            DistrictNumber = districtNumber,
            Rating = rating,
            Year = 2024,
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
            RaceRating.Tossup => (0.50, 0.50),
            RaceRating.LeanRep => (0.35, 0.65),
            RaceRating.LikelyRep => (0.20, 0.80),
            RaceRating.SolidRep => (0.05, 0.95),
            _ => (0.50, 0.50)
        };
    }

    private static (string demName, string repName) GetCandidateNames(string stateId, RaceType raceType)
    {
        var senateNames = new Dictionary<string, (string dem, string rep)>
        {
            ["AL"] = ("Will Boyd", "Katie Britt"),
            ["AZ"] = ("Ruben Gallego", "Kari Lake"),
            ["CA"] = ("Adam Schiff", "Steve Garvey"),
            ["CT"] = ("Chris Murphy", "Matthew Corey"),
            ["DE"] = ("Lisa Blunt Rochester", "Eric Hansen"),
            ["FL"] = ("Debbie Mucarsel-Powell", "Rick Scott"),
            ["HI"] = ("Mazie Hirono", "Bob McDermott"),
            ["IN"] = ("Valerie McCray", "Jim Banks"),
            ["ME"] = ("Angus King", "Demi Kouzounas"),
            ["MD"] = ("Angela Alsobrooks", "Larry Hogan"),
            ["MA"] = ("Elizabeth Warren", "John Deaton"),
            ["MI"] = ("Elissa Slotkin", "Mike Rogers"),
            ["MN"] = ("Amy Klobuchar", "Royce White"),
            ["MS"] = ("Ty Pinkins", "Roger Wicker"),
            ["MO"] = ("Lucas Kunce", "Josh Hawley"),
            ["MT"] = ("Jon Tester", "Tim Sheehy"),
            ["NE"] = ("Dan Osborn", "Deb Fischer"),
            ["NV"] = ("Jacky Rosen", "Sam Brown"),
            ["NJ"] = ("Andy Kim", "Curtis Bashaw"),
            ["NM"] = ("Martin Heinrich", "Nella Domenici"),
            ["ND"] = ("Katrina Christiansen", "Kevin Cramer"),
            ["OH"] = ("Sherrod Brown", "Bernie Moreno"),
            ["PA"] = ("Bob Casey", "Dave McCormick"),
            ["RI"] = ("Sheldon Whitehouse", "Patricia Morgan"),
            ["TN"] = ("Gloria Johnson", "Marsha Blackburn"),
            ["TX"] = ("Colin Allred", "Ted Cruz"),
            ["UT"] = ("Caroline Gleich", "John Curtis"),
            ["VT"] = ("Bernie Sanders", "Gerald Malloy"),
            ["WV"] = ("Glenn Elliott", "Jim Justice"),
            ["WI"] = ("Tammy Baldwin", "Eric Hovde"),
            ["WY"] = ("Scott Morrow", "John Barrasso")
        };

        var govNames = new Dictionary<string, (string dem, string rep)>
        {
            ["DE"] = ("Matt Meyer", "Mike Ramone"),
            ["IN"] = ("Jennifer McCormick", "Mike Braun"),
            ["MS"] = ("Brandon Presley", "Tate Reeves"),
            ["MO"] = ("Crystal Quade", "Mike Kehoe"),
            ["MT"] = ("Ryan Busse", "Greg Gianforte"),
            ["NH"] = ("Joyce Craig", "Kelly Ayotte"),
            ["NC"] = ("Josh Stein", "Mark Robinson"),
            ["ND"] = ("Merrill Piepkorn", "Kelly Armstrong"),
            ["UT"] = ("Brian King", "Spencer Cox"),
            ["VT"] = ("Esther Charlestin", "Phil Scott"),
            ["WA"] = ("Bob Ferguson", "Dave Reichert"),
            ["WV"] = ("Steve Williams", "Patrick Morrisey")
        };

        if (raceType == RaceType.Senate && senateNames.TryGetValue(stateId, out var senatePair))
            return senatePair;
        if (raceType == RaceType.Governor && govNames.TryGetValue(stateId, out var govPair))
            return govPair;

        // Fallback generic names
        return ($"Democratic Candidate ({stateId})", $"Republican Candidate ({stateId})");
    }

    private static (string demName, string repName) GetDistrictCandidateNames(string stateId, int districtNumber)
    {
        // Generate plausible names for House candidates
        var demFirstNames = new[] { "James", "Maria", "David", "Sarah", "Michael", "Jennifer", "Robert", "Lisa", "John", "Emily" };
        var repFirstNames = new[] { "William", "Patricia", "Thomas", "Elizabeth", "Richard", "Barbara", "Charles", "Susan", "Daniel", "Margaret" };
        var lastNames = new[] { "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez", "Hernandez", "Lopez", "Wilson", "Anderson", "Thomas", "Taylor" };

        var hash = (stateId.GetHashCode() + districtNumber * 17) & 0x7FFFFFFF;
        var demFirst = demFirstNames[hash % demFirstNames.Length];
        var repFirst = repFirstNames[(hash / 10) % repFirstNames.Length];
        var demLast = lastNames[(hash / 100) % lastNames.Length];
        var repLast = lastNames[(hash / 1000) % lastNames.Length];

        return ($"{demFirst} {demLast}", $"{repFirst} {repLast}");
    }
}
