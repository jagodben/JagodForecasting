#!/usr/bin/env python3
"""Builds the candidate photo map from Wikipedia lead images.

Pulls every candidate from the running API, batch-resolves their Wikipedia pages
(50 titles per request, following redirects), and records each page's lead-image
thumbnail plus the page URL (shown as the attribution link). Candidates without
a page or photo are simply absent — the UI falls back to an initials avatar.

Re-run after primaries change the field:
    python tools/fetch_candidate_photos.py
Set FORECAST_API to pull the candidate list from production instead of a local
API (whose DB may lag prod's daily nominee refresh by weeks):
    FORECAST_API=https://api.jagodforecasting.com/api python tools/fetch_candidate_photos.py
Writes election-forecaster-client/src/data/candidatePhotos.json
"""
import json
import os
import re
import ssl
import time
import urllib.parse
import urllib.request

import certifi
from PIL import Image
from io import BytesIO

# The bundled Windows cert store trips on Wikipedia's chain; certifi's bundle doesn't.
SSL_CTX = ssl.create_default_context(cafile=certifi.where())

API = os.environ.get("FORECAST_API", "http://localhost:5000/api")
WIKI = "https://en.wikipedia.org/w/api.php"
WIKIDATA = "https://www.wikidata.org/w/api.php"
OUT = "election-forecaster-client/src/data/candidatePhotos.json"
IMG_DIR = "election-forecaster-client/public/candidates"
AVATAR_PX = 84  # 2x the largest render size (42px), cover-cropped square
THUMB = 256
HEADERS = {"User-Agent": "JagodForecasting/1.0 (candidate photo mapping; jagodben@gmail.com)"}
PLACEHOLDER_PREFIXES = ("TBD ", "Democratic Nominee", "Republican Nominee")
STATE_NAMES = {
    "AL": "Alabama", "AK": "Alaska", "AZ": "Arizona", "AR": "Arkansas", "CA": "California",
    "CO": "Colorado", "CT": "Connecticut", "DE": "Delaware", "FL": "Florida", "GA": "Georgia",
    "HI": "Hawaii", "ID": "Idaho", "IL": "Illinois", "IN": "Indiana", "IA": "Iowa",
    "KS": "Kansas", "KY": "Kentucky", "LA": "Louisiana", "ME": "Maine", "MD": "Maryland",
    "MA": "Massachusetts", "MI": "Michigan", "MN": "Minnesota", "MS": "Mississippi",
    "MO": "Missouri", "MT": "Montana", "NE": "Nebraska", "NV": "Nevada", "NH": "New Hampshire",
    "NJ": "New Jersey", "NM": "New Mexico", "NY": "New York", "NC": "North Carolina",
    "ND": "North Dakota", "OH": "Ohio", "OK": "Oklahoma", "OR": "Oregon", "PA": "Pennsylvania",
    "RI": "Rhode Island", "SC": "South Carolina", "SD": "South Dakota", "TN": "Tennessee",
    "TX": "Texas", "UT": "Utah", "VT": "Vermont", "VA": "Virginia", "WA": "Washington",
    "WV": "West Virginia", "WI": "Wisconsin", "WY": "Wyoming",
}


def get(url):
    req = urllib.request.Request(url, headers=HEADERS)
    with urllib.request.urlopen(req, timeout=60, context=SSL_CTX if url.startswith("https") else None) as r:
        return json.load(r)


def get_bytes(url, attempts=4):
    for i in range(attempts):
        try:
            req = urllib.request.Request(url, headers=HEADERS)
            with urllib.request.urlopen(req, timeout=60, context=SSL_CTX) as r:
                return r.read()
        except urllib.error.HTTPError as e:
            if e.code == 429 and i < attempts - 1:
                time.sleep(45 * (i + 1))  # back off politely; Wikimedia rate-limits bursts
                continue
            raise


def slugify(race_id, name):
    keep = "".join(ch if ch.isalnum() else "-" for ch in f"{race_id}-{name}".lower())
    while "--" in keep:
        keep = keep.replace("--", "-")
    return keep.strip("-")


def save_avatar(url, path):
    """Downloads the Wikimedia thumb and writes an AVATAR_PX cover-cropped square WebP —
    Lanczos-resized here so the browser never has to downscale a 250px+ source into a
    42px circle (its fast path aliases badly, especially at page zoom)."""
    img = Image.open(BytesIO(get_bytes(url))).convert("RGB")
    w, h = img.size
    side = min(w, h)
    img = img.crop(((w - side) // 2, max(0, (h - side) // 4), (w - side) // 2 + side, max(0, (h - side) // 4) + side))
    img = img.resize((AVATAR_PX, AVATAR_PX), Image.LANCZOS)
    img.save(path, "WEBP", quality=82)


def wiki_query(params):
    qs = urllib.parse.urlencode({**params, "format": "json", "formatversion": "2"})
    return get(f"{WIKI}?{qs}")


def fetch_candidates():
    """(raceId, name) for every real (non-placeholder) candidate."""
    out = []
    for rtype in ("Senate", "Governor", "House"):
        for race in get(f"{API}/races?type={rtype}"):
            for c in race.get("candidates") or []:
                name = (c.get("name") or "").strip()
                if not name or any(name.startswith(p) for p in PLACEHOLDER_PREFIXES):
                    continue
                out.append((race["id"], name))
    return out


def page_images(titles):
    """title -> (thumb_url, page_title, wikidata_qid) for titles that have a lead image."""
    found = {}
    for i in range(0, len(titles), 50):
        batch = titles[i : i + 50]
        data = wiki_query({
            "action": "query",
            "titles": "|".join(batch),
            "redirects": "1",
            "prop": "pageimages|pageprops",
            "ppprop": "wikibase_item",
            "piprop": "thumbnail",
            "pithumbsize": str(THUMB),
        })
        # walk each asked title forward through its normalization/redirect hops — a batch
        # can hold both a redirect and its target title, and both must credit the page
        # (a reverse to->from map can only credit one asker per page)
        fwd = {}
        for r in (data["query"].get("normalized") or []) + (data["query"].get("redirects") or []):
            fwd[r["from"]] = r["to"]
        pages = {p["title"]: p for p in data["query"].get("pages") or []}
        for asked in batch:
            title, seen = asked, set()
            while title in fwd and title not in seen:
                seen.add(title)
                title = fwd[title]
            page = pages.get(title)
            if page is None:
                continue
            thumb = (page.get("thumbnail") or {}).get("source")
            qid = (page.get("pageprops") or {}).get("wikibase_item")
            if thumb:
                found[asked] = (thumb, page["title"], qid)
        time.sleep(0.3)
    return found


POLITICAL_CAT = re.compile(
    r"politician|candidate|senator|representative|governor|congress|legislat|mayor|officeholder|attorneys general|treasurer|official",
    re.IGNORECASE)

ELECTION_TITLE = re.compile(
    r"(\b\d{4}\b.*\b(elections?|primar(?:y|ies))\b)|(\b(elections?|primar(?:y|ies))\b.*\b\d{4}\b)",
    re.IGNORECASE)

# Files in election articles that are never a person's portrait, whatever they're named.
PORTRAIT_JUNK = re.compile(r"map|result|county|district|logo|seal|flag|ballot|precinct", re.IGNORECASE)


def political_titles(titles):
    """Subset of article titles carrying a political/candidate category. Wikidata items for
    state-level figures are often sparse (SD's sitting AG has no P39), but their articles
    always categorize as politicians or election candidates."""
    ok = set()
    titles = sorted(set(titles))
    for i in range(0, len(titles), 20):
        batch = titles[i : i + 20]
        params = {
            "action": "query", "titles": "|".join(batch), "redirects": "1",
            # visible categories only — hidden maintenance categories ("Official website
            # different in Wikidata...") would satisfy the political regex on any article
            "prop": "categories", "cllimit": "500", "clshow": "!hidden",
        }
        while True:
            data = wiki_query(params)
            for page in data["query"].get("pages") or []:
                # an election/primary article is never a person's article, however political
                # its categories read — "...House of Representatives elections in Alabama"
                # satisfies the regex through the word "Representatives" alone
                if ELECTION_TITLE.search(page["title"]):
                    continue
                for cat in page.get("categories") or []:
                    if POLITICAL_CAT.search(cat.get("title", "")):
                        ok.add(page["title"])
                        break
            cont = data.get("continue")
            if not cont:
                break
            params = {**params, **cont}
        time.sleep(0.3)
    return ok


def politician_qids(qids):
    """(qualified, vetoed) subsets of qids. qualified: a human (P31=Q5) who is verifiably
    political — occupation (P106) includes politician, any position held (P39), or any
    candidacy (P3602). "Is a person" alone let the famous namesakes through — Frank Lucas
    (OK-3) resolved to the drug trafficker, who is human but has never held office.
    vetoed: a human who provably cannot be a live 2026 US candidate — died before the
    cycle, or stated citizenship (P27) excludes the US. The veto must override a
    category-based pass: "Adam Hamilton (politician)" genuinely is a politician — a New
    Zealand one who died in 1952. Better no photo than the wrong person's."""
    POLITICIAN, US = "Q82955", "Q30"
    ok, veto = set(), set()
    ids = sorted({q for q in qids if q})
    for i in range(0, len(ids), 50):
        batch = ids[i : i + 50]
        qs = urllib.parse.urlencode({
            "action": "wbgetentities", "ids": "|".join(batch),
            "props": "claims", "format": "json",
        })
        data = get(f"{WIKIDATA}?{qs}")
        for qid, ent in (data.get("entities") or {}).items():
            claims = ent.get("claims") or {}
            def ids_of(prop):
                out = set()
                for c in claims.get(prop) or []:
                    val = (((c.get("mainsnak") or {}).get("datavalue") or {}).get("value") or {})
                    if isinstance(val, dict) and val.get("id"):
                        out.add(val["id"])
                return out
            if "Q5" not in ids_of("P31"):
                continue
            death_year = None
            for c in claims.get("P570") or []:
                t = (((c.get("mainsnak") or {}).get("datavalue") or {}).get("value") or {}).get("time")
                if t:  # '+1952-04-29T00:00:00Z'
                    death_year = int(t[1:5])
                    break
            citizenships = ids_of("P27")
            if (death_year is not None and death_year < 2025) or (citizenships and US not in citizenships):
                veto.add(qid)
                continue
            if (POLITICIAN in ids_of("P106")
                    or bool(claims.get("P39"))     # any position held
                    or bool(claims.get("P3602"))):  # any candidacy in an election
                ok.add(qid)
        time.sleep(0.3)
    return ok, veto


def file_thumb(file_title):
    """Thumb URL for a File: title (the enwiki API serves Commons-hosted files too)."""
    data = wiki_query({"action": "query", "titles": file_title,
                       "prop": "imageinfo", "iiprop": "url", "iiurlwidth": str(THUMB)})
    for page in data["query"].get("pages") or []:
        info = (page.get("imageinfo") or [{}])[0]
        return info.get("thumburl") or info.get("url")
    return None


def election_portrait(name, article_title):
    """Portrait of `name` embedded in the election article their bare name redirects to.
    Candidates without a standalone article often still have an infobox portrait there,
    and the redirect itself is the identity assertion — Wikipedia routes that exact name
    to that election. Only files carrying the candidate's full name qualify."""
    data = wiki_query({"action": "query", "titles": article_title, "prop": "images", "imlimit": "500"})
    target = name.lower()
    for page in data["query"].get("pages") or []:
        matches = [im["title"] for im in page.get("images") or []
                   if target in im["title"].lower()
                   and im["title"].lower().endswith((".jpg", ".jpeg", ".png"))
                   and not PORTRAIT_JUNK.search(im["title"])]
        if matches:
            thumb = file_thumb(min(matches, key=len))
            if thumb:
                return thumb, page["title"], None
    return None


def linked_from_any(article_titles, target_title):
    """Whether any of the given articles links to target_title (counting links written
    through target's redirects). A 2026 election article naming the matched person is the
    identity proof a name-based Wikidata match lacks on its own."""
    if not article_titles:
        return False
    data = wiki_query({"action": "query", "titles": target_title, "prop": "redirects", "rdlimit": "100"})
    variants = {target_title}
    for page in data["query"].get("pages") or []:
        for r in page.get("redirects") or []:
            variants.add(r["title"])
    data = wiki_query({"action": "query", "titles": "|".join(sorted(article_titles)),
                       "redirects": "1", "prop": "links",
                       "pltitles": "|".join(sorted(variants)[:50]), "pllimit": "500"})
    return any(page.get("links") for page in data["query"].get("pages") or [])


def _claim_year(claims, prop):
    for c in claims.get(prop) or []:
        t = (((c.get("mainsnak") or {}).get("datavalue") or {}).get("value") or {}).get("time")
        if t:  # '+1982-06-11T00:00:00Z'
            return int(t[1:5])
    return None


def wikidata_portrait(name, election_articles):
    """Photo via Wikidata entity search, for names no article title reaches: either the
    item's enwiki article under a title we never guessed, or — when the article was
    deleted or never written — the item's own Commons image (P18).

    The entity search is fuzzy and namesakes are everywhere, so every route must prove
    identity, not just political status: the search match must be the exact name, an
    article hit must be linked from one of this candidate's 2026 election articles, and a
    Commons image is only trusted for an explicit US citizen with a recorded candidacy
    (P3602) or a birth year a living candidate could have — Wikidata is full of
    politically-qualified, photographed state legislators born in the 1800s whose items
    never record a death. Two qualifying namesakes forfeit the match entirely."""
    qs = urllib.parse.urlencode({"action": "wbsearchentities", "search": name, "language": "en",
                                 "type": "item", "limit": "5", "format": "json"})
    ids = [e["id"] for e in (get(f"{WIKIDATA}?{qs}").get("search")) or []
           if ((e.get("match") or {}).get("text") or "").lower() == name.lower()]
    if not ids:
        return None
    ok, veto = politician_qids(ids)
    qs = urllib.parse.urlencode({"action": "wbgetentities", "ids": "|".join(ids),
                                 "props": "claims|sitelinks", "format": "json"})
    entities = (get(f"{WIKIDATA}?{qs}").get("entities")) or {}
    hits = []
    for qid in ids:
        ent = entities.get(qid) or {}
        if qid in veto:
            continue
        claims = ent.get("claims") or {}
        sitelink = ((ent.get("sitelinks") or {}).get("enwiki") or {}).get("title")
        if sitelink:
            hit = page_images([sitelink]).get(sitelink)
            if hit:
                hit_ok, hit_veto = politician_qids([hit[2]])
                if (hit[2] not in hit_veto
                        and (hit[2] in hit_ok or hit[1] in political_titles([hit[1]]))
                        and linked_from_any(election_articles, hit[1])):
                    hits.append(hit)
                    continue
                if hit[1] == sitelink:
                    # a standing article whose identity check failed — the item's P18 is
                    # that same person, and must not re-enter through the Commons route
                    # (a sitelink that REDIRECTED elsewhere means no article of their own,
                    # so the Commons route below stays open for those)
                    continue
        us_citizen = any((((c.get("mainsnak") or {}).get("datavalue") or {}).get("value") or {}).get("id") == "Q30"
                         for c in claims.get("P27") or [])
        credible = bool(claims.get("P3602")) or (_claim_year(claims, "P569") or 0) >= 1936
        if qid in ok and us_citizen and credible:
            p18 = (((claims.get("P18") or [{}])[0].get("mainsnak") or {})
                   .get("datavalue") or {}).get("value")
            if p18:
                thumb = file_thumb(f"File:{p18}")
                if thumb:
                    hits.append((thumb, "https://commons.wikimedia.org/wiki/File:" + urllib.parse.quote(p18.replace(" ", "_")), qid))
    return hits[0] if len(hits) == 1 else None


def race_election_articles(race_id):
    """The 2026 election article title(s) covering a race — the cross-reference target for
    identity-ambiguous Wikidata matches. Both plural/singular House forms and the special-
    election Senate form are offered; nonexistent titles simply never match."""
    state = STATE_NAMES.get(race_id[:2])
    if not state:
        return []
    kind = race_id.split("-")[1]
    if kind == "SEN":
        return [f"2026 United States Senate election in {state}",
                f"2026 United States Senate special election in {state}"]
    if kind == "GOV":
        return [f"2026 {state} gubernatorial election"]
    return [f"2026 United States House of Representatives elections in {state}",
            f"2026 United States House of Representatives election in {state}"]


def main():
    candidates = fetch_candidates()
    names = sorted({name for _, name in candidates})
    print(f"{len(candidates)} candidate slots, {len(names)} unique names")

    resolved = page_images(names)

    # Second pass: politician-suffixed titles for names that missed (disambiguation pages
    # have no lead image, and plain "John Smith" often isn't the politician).
    missing = [n for n in names if n not in resolved]
    if missing:
        retried = page_images([f"{n} (politician)" for n in missing] +
                              [f"{n} (American politician)" for n in missing])
        for n in missing:
            hit = retried.get(f"{n} (politician)") or retried.get(f"{n} (American politician)")
            if hit:
                resolved[n] = hit

    # Identity check on the name-based matches BEFORE the state pass, so a famous namesake
    # (human but not a politician) is rejected and the name falls through to the state-
    # qualified retry that finds the actual politician's article.
    politicians, vetoed = politician_qids([qid for _, _, qid in resolved.values()])
    political_pages = political_titles([title for _, title, _ in resolved.values()])
    def is_political(entry):
        _, title, qid = entry
        return qid not in vetoed and (qid in politicians or title in political_pages)
    dropped = [n for n, v in resolved.items() if not is_political(v)]
    dropped_hits = {n: resolved[n] for n in dropped}
    resolved = {n: v for n, v in resolved.items() if is_political(v)}
    if dropped:
        print(f"rejected {len(dropped)} non-politician matches: {dropped}")

    # Rejected namesakes retry the "(politician)" form before the state-qualified pass —
    # the Wisconsin congressman lives at "Scott Fitzgerald (politician)" while his bare
    # name leads to the novelist.
    retry2 = page_images([f"{n} (politician)" for n in dropped] +
                         [f"{n} (American politician)" for n in dropped])
    ok2, veto2 = politician_qids([qid for _, _, qid in retry2.values()])
    pages2 = political_titles([title for _, title, _ in retry2.values()])
    for n in dropped:
        hit = retry2.get(f"{n} (politician)") or retry2.get(f"{n} (American politician)")
        if hit and hit[2] not in veto2 and (hit[2] in ok2 or hit[1] in pages2):
            resolved[n] = hit

    # Per-race pass: state-qualified titles for everything still unmatched — disambiguated
    # names ("Mike Rogers (Michigan politician)") and the rejected namesakes alike.
    slot_resolved = {}
    still = [(rid, n) for rid, n in candidates if n not in resolved]
    state_titles = {}
    for rid, n in still:
        state = STATE_NAMES.get(rid[:2])
        if state:
            state_titles.setdefault(f"{n} ({state} politician)", []).append((rid, n))
    if state_titles:
        hits = page_images(sorted(state_titles))
        ok, veto = politician_qids([qid for _, _, qid in hits.values()])
        pages3 = political_titles([title for _, title, _ in hits.values()])
        for title, slots in state_titles.items():
            hit = hits.get(title)
            if hit and hit[2] not in veto and (hit[2] in ok or hit[1] in pages3):
                for slot in slots:
                    slot_resolved[slot] = hit
    if slot_resolved:
        print(f"state-qualified matches: {len(slot_resolved)}, e.g.: {[f'{r}|{n}' for r, n in list(slot_resolved)[:5]]}")

    # Last-chance passes for names still unmatched anywhere: a portrait embedded in the
    # election article the bare name redirects to, then Wikidata's own image for people
    # whose article was deleted or never written.
    covered_by_slot = {n for _, n in slot_resolved}
    articles_by_name = {}
    for rid, n in candidates:
        articles_by_name.setdefault(n, set()).update(race_election_articles(rid))
    recovered = {}
    for n in names:
        if n in resolved or n in covered_by_slot:
            continue
        hit = None
        prior = dropped_hits.get(n)
        if prior is not None and ELECTION_TITLE.search(prior[1]):
            hit = election_portrait(n, prior[1])
        if hit is None:
            hit = wikidata_portrait(n, articles_by_name.get(n) or set())
        if hit is not None:
            recovered[n] = hit
        time.sleep(0.3)
    if recovered:
        print(f"fallback matches (election-article portrait / Wikidata image): {sorted(recovered)}")
        resolved.update(recovered)

    os.makedirs(IMG_DIR, exist_ok=True)
    previous = {}
    if os.path.exists(OUT):
        previous = json.load(open(OUT, encoding="utf-8"))
    photos = {}
    failures = 0
    saved_by_name = {}
    for race_id, name in candidates:
        hit = slot_resolved.get((race_id, name)) or resolved.get(name)
        if hit is None:
            continue
        thumb, title, _ = hit
        slug = slugify(race_id, name)
        path = os.path.join(IMG_DIR, f"{slug}.webp")
        # Wikipedia hits carry an article title; fallback hits may carry a full URL
        # (e.g. a Commons file page) that is the attribution link as-is.
        page_url = title if title.startswith("http") else \
            "https://en.wikipedia.org/wiki/" + urllib.parse.quote(title.replace(" ", "_"))
        # A slot whose source article OR source image changed must re-download — the same
        # election article can first yield its lead image (a state seal) and later a
        # name-matched portrait file, so the page alone can't prove the file is current.
        prev = previous.get(f"{race_id}|{name}")
        if prev and (prev.get("page") != page_url or prev.get("src") != thumb) and os.path.exists(path):
            os.remove(path)
            saved_by_name.pop(name, None)
        try:
            # one download per unique person; copy for repeat names in other races
            # (never for state-qualified matches — same name, different person)
            if name in saved_by_name and (race_id, name) not in slot_resolved:
                import shutil
                shutil.copyfile(saved_by_name[name], path)
            elif not os.path.exists(path):
                save_avatar(thumb, path)
                saved_by_name[name] = path
                time.sleep(1.0)
            else:
                saved_by_name.setdefault(name, path)
        except Exception as e:
            failures += 1
            print(f"  avatar failed for {name}: {e}")
            continue
        photos[f"{race_id}|{name}"] = {
            "photo": f"/candidates/{slug}.webp",
            "page": page_url,
            "src": thumb,
        }
    if failures:
        print(f"{failures} avatar downloads failed (left as fallback)")

    # Remove avatar files no longer referenced by the map (dropped wrong-person matches).
    referenced = {v["photo"].rsplit("/", 1)[-1] for v in photos.values()}
    for f in os.listdir(IMG_DIR):
        if f.endswith(".webp") and f not in referenced:
            os.remove(os.path.join(IMG_DIR, f))
            print(f"  removed orphaned avatar {f}")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(photos, f, indent=1, sort_keys=True)

    covered = len(photos)
    print(f"photos for {covered}/{len(candidates)} slots ({covered / len(candidates):.0%}) -> {OUT}")
    by_kind = {"statewide": [0, 0], "house": [0, 0]}
    for race_id, name in candidates:
        kind = "statewide" if ("-SEN" in race_id or "-GOV" in race_id) else "house"
        by_kind[kind][1] += 1
        if f"{race_id}|{name}" in photos:
            by_kind[kind][0] += 1
    for kind, (got, total) in by_kind.items():
        print(f"  {kind}: {got}/{total} ({got / total:.0%})")


if __name__ == "__main__":
    main()
