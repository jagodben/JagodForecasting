using ElectionForecaster.Core.Enums;
using ElectionForecaster.Core.Models;

namespace ElectionForecaster.Infrastructure.Data;

public static partial class ElectionDataProvider
{
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
            ("FL", "Florida", 30, 28, RaceRating.LeanRep, true, true),
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
            ("OH", "Ohio", 17, 15, RaceRating.LeanRep, true, true),
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

        // Add House races for each district. Ratings here are placeholders; StateService
        // replaces them with the real per-district forecasts at startup.
        for (int i = 1; i <= districts; i++)
        {
            var district = new District
            {
                Id = $"{id}-{i:D2}",
                StateId = id,
                Number = i,
                Rating = rating
            };

            var houseRace = CreateHouseRace(id, i, rating);
            district.HouseRace = houseRace;
            state.Races.Add(houseRace);
            state.Districts.Add(district);
        }

        return state;
    }

    private static Race CreateSenateRace(string stateId, RaceRating stateRating)
    {
        SenateNominees.TryGetValue(stateId, out var nominees);
        var raceId = $"{stateId}-SEN-2026";
        var (demName, demParty, demIncumbent) = ResolveChallenger(raceId, nominees.Dem);
        var (repName, repIncumbent) = ResolveNominee(nominees.Rep, "Republican Nominee");
        var (demProb, repProb) = GetProbabilities(stateRating);

        return new Race
        {
            Id = raceId,
            StateId = stateId,
            Type = RaceType.Senate,
            Rating = stateRating,
            Year = 2026,
            IsSpecialElection = SpecialSenateStates.Contains(stateId),
            Candidates = new List<Candidate>
            {
                new() { Id = $"{stateId}-SEN-D", Name = demName, Party = demParty, IsIncumbent = demIncumbent },
                new() { Id = $"{stateId}-SEN-R", Name = repName, Party = Party.Republican, IsIncumbent = repIncumbent }
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
        GovernorNominees.TryGetValue(stateId, out var nominees);
        var raceId = $"{stateId}-GOV-2026";
        var (demName, demParty, demIncumbent) = ResolveChallenger(raceId, nominees.Dem);
        var (repName, repIncumbent) = ResolveNominee(nominees.Rep, "Republican Nominee");
        var (demProb, repProb) = GetProbabilities(stateRating);

        return new Race
        {
            Id = raceId,
            StateId = stateId,
            Type = RaceType.Governor,
            Rating = stateRating,
            Year = 2026,
            Candidates = new List<Candidate>
            {
                new() { Id = $"{stateId}-GOV-D", Name = demName, Party = demParty, IsIncumbent = demIncumbent },
                new() { Id = $"{stateId}-GOV-R", Name = repName, Party = Party.Republican, IsIncumbent = repIncumbent }
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
        HouseNominees.TryGetValue($"{stateId}-{districtNumber:D2}", out var nominees);
        var raceId = $"{stateId}-{districtNumber:D2}-2026";
        var (demName, demParty, demIncumbent) = ResolveChallenger(raceId, nominees.Dem);
        var (repName, repIncumbent) = ResolveNominee(nominees.Rep, "Republican Nominee");
        var (demProb, repProb) = GetProbabilities(rating);

        return new Race
        {
            Id = raceId,
            StateId = stateId,
            Type = RaceType.House,
            DistrictNumber = districtNumber,
            Rating = rating,
            Year = 2026,
            Candidates = new List<Candidate>
            {
                new() { Id = $"{stateId}-{districtNumber:D2}-D", Name = demName, Party = demParty, IsIncumbent = demIncumbent },
                new() { Id = $"{stateId}-{districtNumber:D2}-R", Name = repName, Party = Party.Republican, IsIncumbent = repIncumbent }
            },
            Forecasts = new List<Forecast>
            {
                new() { CandidateId = $"{stateId}-{districtNumber:D2}-D", CandidateName = demName, WinProbability = demProb, ProjectedVoteShare = 0.45 + (demProb - 0.5) * 0.2 },
                new() { CandidateId = $"{stateId}-{districtNumber:D2}-R", CandidateName = repName, WinProbability = repProb, ProjectedVoteShare = 0.45 + (repProb - 0.5) * 0.2 }
            }
        };
    }

    /// <summary>A confirmed general-election nominee and whether they are the incumbent.</summary>
    private sealed record Nominee(string Name, bool IsIncumbent);

    private static (string Name, bool Incumbent) ResolveNominee(Nominee? nominee, string placeholder)
        => nominee is null ? (placeholder, false) : (nominee.Name, nominee.IsIncumbent);

    /// <summary>
    /// The challenger-slot candidate for a race: a designated viable independent if one exists for
    /// this race (displacing the token Democrat), otherwise the Democratic nominee/placeholder. The
    /// challenger slot keeps its "-D" id and carries the forecast's Dem-side probability regardless of
    /// party, so the independent flows through the two-way engine while showing its real party in the UI.
    /// </summary>
    private static (string Name, Party Party, bool Incumbent) ResolveChallenger(string raceId, Nominee? demNominee)
    {
        var independent = IndependentChallengers.Get(raceId);
        if (independent is { ReplacesDem: true })
            return (independent.Name, Party.Independent, false);

        var (name, incumbent) = ResolveNominee(demNominee, "Democratic Nominee");
        return (name, Party.Democrat, incumbent);
    }

    // -----------------------------------------------------------------------------------------
    // 2026 nominees — sourced from Wikipedia as of 2026-07-02. A name is filled in only where
    // that party's nominating contest has CONCLUDED (primary/runoff held before this date);
    // races whose primaries are still upcoming (e.g. Michigan Aug 4, Arizona Jul 21, Wyoming
    // Aug 18, New Hampshire Sep 8) are intentionally left out and fall back to the generic
    // "Democratic Nominee" / "Republican Nominee" placeholder. Down-ballot challenger names in
    // safe states are lower-confidence and worth spot-checking. Update as more primaries conclude.
    // -----------------------------------------------------------------------------------------
    // The 2026 FL and OH U.S. Senate contests are special elections — Rubio (FL) left for Secretary
    // of State and Vance (OH) for Vice President, and both seats are held by appointed Republicans.
    private static readonly HashSet<string> SpecialSenateStates = new() { "FL", "OH" };

    private static readonly Dictionary<string, (Nominee? Dem, Nominee? Rep)> SenateNominees = new()
    {
        ["AL"] = (new("Everett Wess", false), new("Barry Moore", false)),        // open (Tuberville → gov)
        ["AR"] = (new("Hallie Shoffner", false), new("Tom Cotton", true)),
        ["CO"] = (new("John Hickenlooper", true), new("Mark Baisley", false)),
        ["FL"] = (null, new("Ashley Moody", true)),                              // special; Moody appointed, Dem primary Aug 18
        ["GA"] = (new("Jon Ossoff", true), new("Mike Collins", false)),
        ["ID"] = (new("David Roth", false), new("Jim Risch", true)),
        ["IL"] = (new("Juliana Stratton", false), new("Don Tracy", false)),      // open (Durbin retiring)
        ["IA"] = (new("Josh Turek", false), new("Ashley Hinson", false)),        // open (Ernst not running)
        ["KY"] = (new("Charles Booker", false), new("Andy Barr", false)),        // open (McConnell retiring)
        ["LA"] = (new("Jamie Davis", false), new("Julia Letlow", false)),        // Cassidy lost primary
        ["ME"] = (new("Graham Platner", false), new("Susan Collins", true)),
        ["MS"] = (new("Scott Colom", false), new("Cindy Hyde-Smith", true)),
        ["MT"] = (new("Alani Bankhead", false), new("Kurt Alme", false)),        // Daines withdrew
        ["NE"] = (new("Cindy Burbank", false), new("Pete Ricketts", true)),
        ["NJ"] = (new("Cory Booker", true), new("Justin Murphy", false)),
        ["NM"] = (new("Ben Ray Luján", true), new("Larry Marker", false)),
        ["NC"] = (new("Roy Cooper", false), new("Michael Whatley", false)),      // open (Tillis retiring)
        ["OH"] = (new("Sherrod Brown", false), new("Jon Husted", true)),         // special; Husted appointed
        ["OK"] = (null, new("Kevin Hern", false)),                               // Dem runoff Aug 25 (open)
        ["OR"] = (new("Jeff Merkley", true), new("David Brock Smith", false)),
        ["SC"] = (new("Annie Andrews", false), new("Lindsey Graham", true)),
        ["SD"] = (new("Julian Beaudion", false), new("Mike Rounds", true)),
        ["TX"] = (new("James Talarico", false), new("Ken Paxton", false)),       // Cornyn lost runoff
        ["VA"] = (new("Mark Warner", true), null),                               // Rep primary upcoming
        ["WV"] = (new("Rachel Fetty Anderson", false), new("Shelley Moore Capito", true)),
    };

    private static readonly Dictionary<string, (Nominee? Dem, Nominee? Rep)> GovernorNominees = new()
    {
        ["AL"] = (new("Doug Jones", false), new("Tommy Tuberville", false)),     // open (Ivey term-limited)
        ["AR"] = (new("Fredrick Love", false), new("Sarah Huckabee Sanders", true)),
        ["CA"] = (new("Xavier Becerra", false), new("Steve Hilton", false)),     // open (Newsom term-limited)
        ["CO"] = (new("Phil Weiser", false), new("Barbara Kirkmeyer", false)),   // open (Polis term-limited)
        ["GA"] = (new("Keisha Lance Bottoms", false), new("Rick Jackson", false)), // open (Kemp term-limited)
        ["IL"] = (new("JB Pritzker", true), new("Darren Bailey", false)),
        ["IA"] = (new("Rob Sand", false), new("Zach Lahn", false)),              // open (Reynolds retiring)
        ["MD"] = (new("Wes Moore", true), new("Dan Cox", false)),
        ["ME"] = (new("Hannah Pingree", false), new("Robert B. Charles", false)), // open (Mills term-limited)
        ["NE"] = (new("Lynne Walz", false), new("Jim Pillen", true)),
        ["NV"] = (new("Aaron Ford", false), new("Joe Lombardo", true)),
        ["NM"] = (new("Deb Haaland", false), new("Gregg Hull", false)),          // open (Lujan Grisham term-limited)
        ["NY"] = (new("Kathy Hochul", true), new("Bruce Blakeman", false)),
        ["OH"] = (new("Amy Acton", false), new("Vivek Ramaswamy", false)),       // open (DeWine term-limited)
        ["OK"] = (new("Cyndi Munson", false), new("Gentner Drummond", false)),   // open (Stitt term-limited)
        ["OR"] = (new("Tina Kotek", true), new("Christine Drazan", false)),
        ["PA"] = (new("Josh Shapiro", true), new("Stacy Garrity", false)),
        ["SC"] = (new("Jermaine Johnson", false), new("Alan Wilson", false)),    // open (McMaster term-limited)
        ["SD"] = (new("Dan Ahlers", false), new("Larry Rhoden", true)),
        ["TX"] = (new("Gina Hinojosa", false), new("Greg Abbott", true)),
    };

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
