#!/usr/bin/env python3
"""Builds the forecast-vs-result scorecard from the final forecast and the reported results.

The final forecast is each race's Election Day (Nov 3) snapshot in backups/forecast-export.json.gz.
Results come from each race's election infobox on Wikipedia (nominees, parties, vote shares, the
winner in bold): the state sections of the Senate and gubernatorial overview articles, and the
"District N" sections of each state's House article (following its split sub-articles).

Margins use the model's convention: the leading non-Republican minus the leading Republican, in
points, so they compare directly with the forecast's expected Dem margin (whose challenger slot
can be a viable independent). A race with no Republican or no non-Republican on the ballot (e.g.
a California Dem-vs-Dem top two) still records its winner, so the call can be scored even where
the margin can't.

Re-run as late counts come in:
    python tools/fetch_results.py
Writes election-forecaster-client/public/data/results.json, read by the Results page and the race
and state pages.
"""
import gzip
import json
import re
import ssl
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import date

import certifi

# The bundled Windows cert store trips on Wikipedia's chain; certifi's bundle doesn't.
SSL_CTX = ssl.create_default_context(cafile=certifi.where())

WIKI = "https://en.wikipedia.org/w/api.php"
EXPORT = "backups/forecast-export.json.gz"
OUT = "election-forecaster-client/public/data/results.json"

# Seats not up in 2026 and the majority line, as the chamber simulation counts them
# (MonteCarloSimulator: independents caucusing with Democrats count as Democratic; the Vice
# President breaks a 50-50 Senate for Republicans, so Democrats need 51).
CHAMBER_RULES = {
    "Senate": {"seatsNotUp": {"dem": 34, "rep": 31}, "majority": 51, "totalSeats": 100},
    "House": {"seatsNotUp": {"dem": 0, "rep": 0}, "majority": 218, "totalSeats": 435},
}
FINAL_FORECAST_DATE = "2026-11-03"

STATES = {
    "Alabama": "AL", "Alaska": "AK", "Arizona": "AZ", "Arkansas": "AR", "California": "CA",
    "Colorado": "CO", "Connecticut": "CT", "Delaware": "DE", "Florida": "FL", "Georgia": "GA",
    "Hawaii": "HI", "Idaho": "ID", "Illinois": "IL", "Indiana": "IN", "Iowa": "IA", "Kansas": "KS",
    "Kentucky": "KY", "Louisiana": "LA", "Maine": "ME", "Maryland": "MD", "Massachusetts": "MA",
    "Michigan": "MI", "Minnesota": "MN", "Mississippi": "MS", "Missouri": "MO", "Montana": "MT",
    "Nebraska": "NE", "Nevada": "NV", "New Hampshire": "NH", "New Jersey": "NJ",
    "New Mexico": "NM", "New York": "NY", "North Carolina": "NC", "North Dakota": "ND",
    "Ohio": "OH", "Oklahoma": "OK", "Oregon": "OR", "Pennsylvania": "PA", "Rhode Island": "RI",
    "South Carolina": "SC", "South Dakota": "SD", "Tennessee": "TN", "Texas": "TX", "Utah": "UT",
    "Vermont": "VT", "Virginia": "VA", "Washington": "WA", "West Virginia": "WV",
    "Wisconsin": "WI", "Wyoming": "WY",
}
STATE_NAMES = {v: k for k, v in STATES.items()}

PARTIES = {"Democratic": "Democrat", "Republican": "Republican", "Independent": "Independent",
           "Nonpartisan": "Independent", "Libertarian": "Libertarian", "Green": "Green"}

_cache = {}


def wikitext(title):
    if title not in _cache:
        query = urllib.parse.urlencode({"action": "parse", "prop": "wikitext", "formatversion": 2,
                                        "redirects": 1, "format": "json", "page": title})
        req = urllib.request.Request(f"{WIKI}?{query}",
                                     headers={"User-Agent": "JagodForecasting/1.0 (results scorecard)"})
        for attempt in range(6):
            try:
                with urllib.request.urlopen(req, context=SSL_CTX, timeout=60) as resp:
                    data = json.load(resp)
                break
            except urllib.error.HTTPError as e:
                if e.code != 429 or attempt == 5:
                    raise
                time.sleep(int(e.headers.get("Retry-After") or 0) or 10 * (attempt + 1))
        _cache[title] = data["parse"]["wikitext"] if "parse" in data else None
        time.sleep(1)
    return _cache[title]


def sections(text, level):
    """{heading title: body} for every heading of exactly the given level."""
    heads = list(re.finditer(r"^(=+)\s*(.*?)\s*\1\s*$", text, re.M))
    out = {}
    for i, h in enumerate(heads):
        if len(h.group(1)) != level:
            continue
        end = next((n.start() for n in heads[i + 1:] if len(n.group(1)) <= level), len(text))
        out.setdefault(h.group(2), text[h.end():end])
    return out


def clean(s):
    s = re.sub(r"<ref[^>]*/>|<ref.*?</ref>", "", s, flags=re.S)
    s = re.sub(r"\[\[(?:[^|\]]*\|)?([^\]]*)\]\]", r"\1", s)  # [[Page|Name]] -> Name
    s = re.sub(r"\{\{[^{}]*\}\}|<[^>]+>", "", s)
    s = s.replace("(incumbent)", "")
    return re.sub(r"'''?", "", s).strip()


def infobox(text):
    """The candidates of the first election infobox in the text, in infobox order."""
    start = text.find("{{Infobox election")
    if start < 0:
        return []
    depth, i = 0, start
    while i < len(text):
        if text.startswith("{{", i):
            depth, i = depth + 1, i + 2
        elif text.startswith("}}", i):
            depth, i = depth - 1, i + 2
            if depth == 0:
                break
        else:
            i += 1
    fields = {}
    for m in re.finditer(r"^\s*\|\s*(nominee|candidate|party|popular_vote|percentage|\d+data)(\d+)\s*=(.*)$",
                         text[start:i], re.M):
        fields.setdefault(int(m.group(2)), {})[m.group(1)] = m.group(3).strip()

    # Instant-runoff infoboxes put each round in "<round>data<n>" ("136,337<br />47.53%") instead
    # of percentage<n>. The last round decides the race, so score that; a candidate eliminated
    # before it ("''Eliminated''") drops out of the comparison.
    # A runoff that hasn't happened yet has no numbers, so "last" means the last round reported —
    # and a race still awaiting one has no result to score yet.
    all_rounds = {int(k[:-4]) for f in fields.values() for k in f if k.endswith("data")}
    rounds = sorted({int(k[:-4]) for f in fields.values() for k, v in f.items()
                     if k.endswith("data") and "%" in v})
    runoff_pending = bool(rounds) and max(all_rounds) > rounds[-1]
    if rounds:
        last = f"{rounds[-1]}data"
        for f in fields.values():
            share = re.search(r"([\d.]+)%", f.get(last, ""))
            f["percentage"] = share.group(0) if share else ""
            f["eliminated"] = not share

    cands = []
    for n in sorted(fields):
        f = fields[n]
        raw_name = f.get("nominee") or f.get("candidate") or ""
        name = clean(raw_name)
        if not name or f.get("eliminated"):
            continue
        party_text = clean(f.get("party", ""))
        pct = re.search(r"[\d.]+", clean(f.get("percentage", "")))
        cands.append({
            "name": name,
            "party": next((v for k, v in PARTIES.items() if party_text.startswith(k)), "Other"),
            "pct": float(pct.group()) if pct else None,
            # The infobox bolds the winner once the race is called.
            "won": "'''" in raw_name,
        })
    if runoff_pending:
        for c in cands:
            c["runoffPending"] = True
    return cands


def statewide(overview_title, office):
    """Senate/governor results from the overview article's per-state sections."""
    out = {}
    for title, body in sections(wikitext(overview_title), 2).items():
        state = re.sub(r"\s*\(special\)", "", title).strip()
        if state in STATES:
            out[f"{STATES[state]}-{office}-2026"] = infobox(body)
    return out


def house_district(state, district, at_large):
    if at_large:
        return infobox(wikitext(f"2026 United States House of Representatives election in {STATE_NAMES[state]}") or "")
    parent = wikitext(f"2026 United States House of Representatives elections in {STATE_NAMES[state]}") or ""
    body = sections(parent, 2).get(f"District {district}")
    if body is None:
        # Split by district range: the parent links each "(districts A–B)" sub-article.
        for link in re.finditer(r"\{\{\s*main\s*\|([^}]+)\}\}", parent, re.I):
            for target in link.group(1).split("|"):
                r = re.search(r"\(districts?\s+(\d+)\s*[–—-]\s*(\d+)\)", target)
                if r and int(r.group(1)) <= district <= int(r.group(2)):
                    body = sections(wikitext(target.strip()) or "", 2).get(f"District {district}")
    return infobox(body or "")


# "*{{Party stripe|...}}{{aye}} '''[[Page (disambig)|Name]]''' (Democratic) 54.7%" -- greedy up to
# the LAST parenthesized group, so a link's disambiguator isn't mistaken for the party.
BULLET = re.compile(r"^\*\s*\{\{Party stripe\|[^}]*\}\}(.*)\(([^()]*)\)\s*([\d.]+)?\s*%?", re.I)


def house_overview():
    """Each district's candidate list from the House overview article's per-state tables, which
    editors often update (winner ticks, shares) before the district's own infobox."""
    out = {}
    text = wikitext("2026 United States House of Representatives elections")
    for row in re.split(r"^\|-.*$", text, flags=re.M):
        m = re.search(r"\{\{ushr\|([A-Z]{2})\|(\w+)\|X\}\}", row)
        if not m:
            continue
        cands = []
        for line in row.splitlines():
            b = BULLET.match(line.strip())
            if b:
                party_text = b.group(2)
                cands.append({
                    "name": clean(b.group(1)),
                    "party": next((v for k, v in PARTIES.items() if party_text.startswith(k)), "Other"),
                    "pct": float(b.group(3)) if b.group(3) else None,
                    "won": "{{aye}}" in b.group(1).lower(),
                })
        if cands:
            district = 1 if m.group(2) == "AL" else int(m.group(2))
            out[f"{m.group(1)}-{district:02d}-2026"] = cands
    return out


def merge_overview(cands, overview):
    """Fill what the district infobox hasn't posted yet from the overview row."""
    if not overview:
        return cands
    if any(c["pct"] is None for c in cands) and all(c["pct"] is not None for c in overview):
        return overview
    if not any(c["won"] for c in cands):
        winners = {c["name"] for c in overview if c["won"]}
        for c in cands:
            c["won"] = c["name"] in winners
    return cands


# Ranked-choice states: votes for same-party candidates pool in the runoff, so the model sums each
# party's share (as it does for these states' polls). Without it, an Alaska race with two
# Republicans splitting the vote reads as a blowout the runoff would never produce.
RCV_STATES = {"AK", "ME"}


def score(cands, rcv=False):
    """Result fields for one race from its candidate list."""
    winner = next((c for c in cands if c["won"]), None)
    reps = [c for c in cands if c["party"] == "Republican"]
    others = [c for c in cands if c["party"] != "Republican"]
    result = {
        "winner": winner["name"] if winner else None,
        "winnerParty": winner["party"] if winner else None,
        "candidates": cands,
        "actualMargin": None,
    }
    if any(c.get("runoffPending") for c in cands):
        result["status"] = "runoff"
        result["winner"] = result["winnerParty"] = None
    elif not reps or not others:
        result["status"] = "noRepublican" if not reps else "noChallenger"
    elif reps[0]["pct"] is not None and all(c["pct"] is not None for c in reps + others):
        if rcv:
            by_party = {}
            for c in others:
                by_party[c["party"]] = by_party.get(c["party"], 0) + c["pct"]
            result["actualMargin"] = round(max(by_party.values()) - sum(c["pct"] for c in reps), 2)
        else:
            result["actualMargin"] = round(max(c["pct"] for c in others) - max(c["pct"] for c in reps), 2)
        result["status"] = "final" if winner else "uncalled"
    else:
        result["status"] = "winnerOnly"  # vote shares not posted yet
    return result


def main():
    export = json.load(gzip.open(EXPORT))
    final = {r["raceId"]: r for r in export["forecastHistory"] if r["date"][:10] == FINAL_FORECAST_DATE}

    results = {}
    results.update(statewide("2026 United States Senate elections", "SEN"))
    results.update(statewide("2026 United States gubernatorial elections", "GOV"))

    house_counts = {}
    for race_id in final:
        parts = race_id.split("-")
        if parts[1].isdigit():
            house_counts[parts[0]] = house_counts.get(parts[0], 0) + 1
    overview = house_overview()
    for race_id in sorted(final):
        state, seat, _ = race_id.split("-")
        if seat.isdigit():
            results[race_id] = merge_overview(
                house_district(state, int(seat), house_counts[state] == 1), overview.get(race_id))

    races, missing = {}, []
    for race_id, f in sorted(final.items()):
        cands = results.get(race_id)
        if not cands:
            missing.append(race_id)
            continue
        races[race_id] = {
            "predictedMargin": round(f["expectedDemMargin"], 2),
            "predictedDemWinProbability": round(f["demWinProbability"], 4),
            **score(cands, rcv=race_id[:2] in RCV_STATES),
        }

    # The final chamber forecast: the Election Day run of the seat simulation.
    chambers = {}
    for c in export["chamberHistory"]:
        if c["date"][:10] == FINAL_FORECAST_DATE and c["chamber"] in CHAMBER_RULES:
            chambers[c["chamber"]] = {
                "demControlProbability": round(c["demControlProbability"], 4),
                "expectedDemSeats": round(c["expectedDemSeats"], 2),
                "demSeatsLow": c["demSeatsLow"],
                "demSeatsHigh": c["demSeatsHigh"],
                **CHAMBER_RULES[c["chamber"]],
            }

    with open(OUT, "w", encoding="utf-8", newline="\n") as fh:
        json.dump({"asOf": date.today().isoformat(), "forecastDate": FINAL_FORECAST_DATE,
                   "chambers": chambers, "races": races}, fh, indent=1, sort_keys=True, ensure_ascii=False)

    statuses = {}
    for r in races.values():
        statuses[r["status"]] = statuses.get(r["status"], 0) + 1
    print(f"{len(races)} races written ({statuses}); no result found for {len(missing)}: {missing}")


if __name__ == "__main__":
    main()
