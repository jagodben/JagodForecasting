"""Audits which races have Wikipedia poll tables the model isn't ingesting.

Mirrors WikipediaPollingClient's section/table selection in Python and compares it against the
polls the API actually serves, so a parser blind spot surfaces as a flagged race instead of a
silently empty polling average. This is how the Minnesota "(DFL)" and Nebraska independent-column
gaps were found; rerun it after parser changes, or when a race looks under-polled.

    FORECAST_API=https://api.jagodforecasting.com/api python tools/audit_poll_coverage.py

Flags, and what they usually mean:
  NO D-VS-R TABLE             a party label the parser doesn't recognise (the DFL class)
  INDEPENDENT-ONLY TABLES     the real matchup has no major-party column (the Osborn class)
  INDEPENDENT TABLE SKIPPED   an (I)-vs-(R) table we don't read; fine unless that independent is
                              the race's real challenger, in which case add them to
                              IndependentChallengers so the parser reads their column
  PARSEABLE BUT NONE STORED   rows exist but every one is filtered out or out of window
  NO POLLING SECTION          usually genuine: that race simply has no published polling
"""

import json
import os
import re
import ssl
import time
import urllib.parse
import urllib.request
from collections import defaultdict

import certifi

API = os.environ.get("FORECAST_API", "https://api.jagodforecasting.com/api")
WIKI = "https://en.wikipedia.org/w/api.php"
UA = {"User-Agent": "JagodForecasting-audit/1.0 (https://jagodforecasting.com)"}
# The bundled Windows cert store trips on Wikipedia's chain; certifi's bundle doesn't.
CTX = ssl.create_default_context(cafile=certifi.where())

STATE_NAMES = {
    "AL": "Alabama", "AK": "Alaska", "AZ": "Arizona", "AR": "Arkansas", "CA": "California",
    "CO": "Colorado", "CT": "Connecticut", "DE": "Delaware", "FL": "Florida", "GA": "Georgia",
    "HI": "Hawaii", "ID": "Idaho", "IL": "Illinois", "IN": "Indiana", "IA": "Iowa",
    "KS": "Kansas", "KY": "Kentucky", "LA": "Louisiana", "ME": "Maine", "MD": "Maryland",
    "MA": "Massachusetts", "MI": "Michigan", "MN": "Minnesota", "MS": "Mississippi",
    "MO": "Missouri", "MT": "Montana", "NE": "Nebraska", "NV": "Nevada",
    "NH": "New Hampshire", "NJ": "New Jersey", "NM": "New Mexico", "NY": "New York",
    "NC": "North Carolina", "ND": "North Dakota", "OH": "Ohio", "OK": "Oklahoma",
    "OR": "Oregon", "PA": "Pennsylvania", "RI": "Rhode Island", "SC": "South Carolina",
    "SD": "South Dakota", "TN": "Tennessee", "TX": "Texas", "UT": "Utah", "VT": "Vermont",
    "VA": "Virginia", "WA": "Washington", "WV": "West Virginia", "WI": "Wisconsin",
    "WY": "Wyoming",
}

# Party labels the parser maps to a national party; anything else makes a column invisible.
PARTY_LABELS = {"D": "D", "DFL": "D", "D-NPL": "D", "R": "R", "I": "I"}


def get_json(url):
    req = urllib.request.Request(url, headers=UA)
    return json.load(urllib.request.urlopen(req, timeout=60, context=CTX))


def page_title(race_id):
    parts = race_id.split("-")
    state = STATE_NAMES.get(parts[0])
    if not state:
        return None
    if parts[1] == "SEN":
        return "2026 United States Senate election in " + state
    if parts[1] == "GOV":
        return "2026 " + state + " gubernatorial election"
    return None


def fetch_wikitext(titles):
    """title -> wikitext, in batches (the API takes many titles per call)."""
    out = {}
    for i in range(0, len(titles), 20):
        params = {
            "action": "query", "prop": "revisions", "rvprop": "content", "rvslots": "main",
            "format": "json", "formatversion": "2", "redirects": "1",
            "titles": "|".join(titles[i:i + 20]),
        }
        query = get_json(WIKI + "?" + urllib.parse.urlencode(params)).get("query", {})
        # Follow normalisations/redirects back to the title we asked for.
        alias = {n["to"]: n["from"] for n in query.get("normalized", [])}
        for r in query.get("redirects", []):
            alias[r["to"]] = alias.get(r["from"], r["from"])
        for page in query.get("pages", []):
            key = alias.get(page.get("title"), page.get("title"))
            out[key] = None if "missing" in page else page["revisions"][0]["slots"]["main"]["content"]
        time.sleep(0.4)
    return out


def section_block(wikitext, name, level):
    """The body under a heading of the given level, up to the next same-or-higher heading."""
    eq = "=" * level
    pattern = re.compile(r"^\s*" + eq + r"\s*([^=]+?)\s*" + eq + r"\s*$", re.M)
    for match in pattern.finditer(wikitext):
        if match.group(1).strip().lower() != name.lower():
            continue
        body = wikitext[match.end():]
        for nxt in re.finditer(r"^\s*(={2,6})\s*[^=]+?\s*\1\s*$", body, re.M):
            if len(nxt.group(1)) <= level:
                return body[:nxt.start()]
        return body
    return None


def tables(block):
    """Each wikitable in the block, matching nested tables by depth."""
    out, idx = [], 0
    while True:
        start = block.find("{|", idx)
        if start < 0:
            return out
        depth, i = 0, start
        while i < len(block) - 1:
            if block[i:i + 2] == "{|":
                depth += 1
                i += 2
                continue
            if block[i:i + 2] == "|}":
                depth -= 1
                i += 2
                if depth == 0:
                    break
                continue
            i += 1
        out.append(block[start:i])
        idx = i


def clean(text):
    text = re.sub(r"<ref[^>]*?/>", "", text)
    text = re.sub(r"<ref[^>]*?>.*?</ref>", "", text, flags=re.S)
    prev = None
    while prev != text:
        prev, text = text, re.sub(r"\{\{[^{}]*\}\}", "", text)
    text = re.sub(r"\[\[[^\]|]*\|([^\]]*)\]\]", r"\1", text)
    text = re.sub(r"\[\[([^\]]*)\]\]", r"\1", text)
    text = text.replace("'" * 3, "").replace("'" * 2, "")
    text = re.sub(r"<br\s*/?>", " ", text, flags=re.I)
    text = re.sub(r"<[^>]+>", "", text)
    return re.sub(r"\s+", " ", text.replace("&nbsp;", " ")).strip()


def strip_attrs(cell):
    """Wikitable cells are "attributes | content"; split on the first top-level pipe."""
    brace = bracket = 0
    for i, ch in enumerate(cell):
        if cell[i:i + 2] == "{{":
            brace += 1
        elif cell[i:i + 2] == "}}":
            brace = max(0, brace - 1)
        elif cell[i:i + 2] == "[[":
            bracket += 1
        elif cell[i:i + 2] == "]]":
            bracket = max(0, bracket - 1)
        elif ch == "|" and brace == 0 and bracket == 0:
            return cell[i + 1:]
    return cell


def headers_of(table):
    heads = []
    for raw in table.split("\n"):
        line = raw.rstrip()
        if line.startswith("!"):
            parts = line[1:].split("!!") if "!!" in line else [line[1:]]
            heads += [clean(strip_attrs(p)) for p in parts]
        elif line.startswith("|-") and heads:
            break
    return heads


def parties_in(headers):
    found = set()
    for header in headers:
        match = re.search(r"\(\s*([A-Za-z][A-Za-z\-]*)\s*\)\s*$", header)
        if match and match.group(1).upper() in PARTY_LABELS:
            found.add(PARTY_LABELS[match.group(1).upper()])
    return found


def main():
    races = []
    for race_type in ("Senate", "Governor"):
        races += [r["id"] for r in get_json(API + "/races?type=" + race_type)]

    stored = defaultdict(int)
    for poll in get_json(API + "/forecast/polls"):
        stored[poll["raceId"]] += 1

    titles = {race: page_title(race) for race in races}
    wikitext = fetch_wikitext(sorted({t for t in titles.values() if t}))

    issues = []
    for race in sorted(races):
        text = wikitext.get(titles[race])
        if not text:
            issues.append((race, "PAGE MISSING", ""))
            continue

        general = section_block(text, "General election", 2)
        polling = (section_block(general, "Polling", 3) if general
                   else section_block(text, "Polling", 2))
        if not polling:
            issues.append((race, "NO POLLING SECTION", ""))
            continue

        kinds = []
        for table in tables(polling):
            headers = headers_of(table)
            if any("aggregation" in h.lower() for h in headers):
                continue  # aggregator table; we compute our own average
            found = parties_in(headers)
            if found:
                kinds.append("".join(sorted(found)))

        if not kinds:
            continue
        parseable = sum(1 for k in kinds if "D" in k and "R" in k)
        independent_only = [k for k in kinds if "I" in k and not ("D" in k and "R" in k)]

        if parseable == 0 and independent_only:
            issues.append((race, "INDEPENDENT-ONLY TABLES", "tables=%s stored=%d" % (kinds, stored[race])))
        elif parseable == 0:
            issues.append((race, "NO D-VS-R TABLE", "tables=%s stored=%d" % (kinds, stored[race])))
        elif stored[race] == 0:
            issues.append((race, "PARSEABLE BUT NONE STORED", "tables=%s" % (kinds,)))
        elif independent_only:
            issues.append((race, "INDEPENDENT TABLE SKIPPED", "tables=%s stored=%d" % (kinds, stored[race])))

    print("%d statewide races checked, %d flagged\n" % (len(races), len(issues)))
    for race, kind, detail in issues:
        print("%-16s %-28s %s" % (race, kind, detail))


if __name__ == "__main__":
    main()
