#!/usr/bin/env python3
"""Builds the candidate photo map from Wikipedia lead images.

Pulls every candidate from the running API, batch-resolves their Wikipedia pages
(50 titles per request, following redirects), and records each page's lead-image
thumbnail plus the page URL (shown as the attribution link). Candidates without
a page or photo are simply absent — the UI falls back to an initials avatar.

Re-run after primaries change the field:
    python tools/fetch_candidate_photos.py
Writes election-forecaster-client/src/data/candidatePhotos.json
"""
import json
import os
import ssl
import time
import urllib.parse
import urllib.request

import certifi
from PIL import Image
from io import BytesIO

# The bundled Windows cert store trips on Wikipedia's chain; certifi's bundle doesn't.
SSL_CTX = ssl.create_default_context(cafile=certifi.where())

API = "http://localhost:5000/api"
WIKI = "https://en.wikipedia.org/w/api.php"
WIKIDATA = "https://www.wikidata.org/w/api.php"
OUT = "election-forecaster-client/src/data/candidatePhotos.json"
IMG_DIR = "election-forecaster-client/public/candidates"
AVATAR_PX = 84  # 2x the largest render size (42px), cover-cropped square
THUMB = 256
HEADERS = {"User-Agent": "JagodForecasting/1.0 (candidate photo mapping; jagodben@gmail.com)"}
PLACEHOLDER_PREFIXES = ("TBD ", "Democratic Nominee", "Republican Nominee")


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
        # map redirected/normalized titles back to what we asked for
        back = {}
        for r in (data["query"].get("normalized") or []) + (data["query"].get("redirects") or []):
            back[r["to"]] = back.get(r["from"], r["from"])
        for page in data["query"].get("pages") or []:
            asked = back.get(page["title"], page["title"])
            thumb = (page.get("thumbnail") or {}).get("source")
            qid = (page.get("pageprops") or {}).get("wikibase_item")
            if thumb:
                found[asked] = (thumb, page["title"], qid)
        time.sleep(0.3)
    return found


def human_qids(qids):
    """Subset of qids whose Wikidata item is instance-of human (Q5). A name that lands on
    a seal, an office, or a list page fails this — better no photo than the wrong one."""
    humans = set()
    ids = sorted({q for q in qids if q})
    for i in range(0, len(ids), 50):
        batch = ids[i : i + 50]
        qs = urllib.parse.urlencode({
            "action": "wbgetentities", "ids": "|".join(batch),
            "props": "claims", "format": "json",
        })
        data = get(f"{WIKIDATA}?{qs}")
        for qid, ent in (data.get("entities") or {}).items():
            for claim in (ent.get("claims") or {}).get("P31") or []:
                val = (((claim.get("mainsnak") or {}).get("datavalue") or {}).get("value") or {})
                if val.get("id") == "Q5":
                    humans.add(qid)
                    break
        time.sleep(0.3)
    return humans


def main():
    candidates = fetch_candidates()
    names = sorted({name for _, name in candidates})
    print(f"{len(candidates)} candidate slots, {len(names)} unique names")

    resolved = page_images(names)

    # Second pass: politician-suffixed titles for names that missed (disambiguation pages
    # have no lead image, and plain "John Smith" often isn't the politician).
    missing = [n for n in names if n not in resolved]
    retry_titles = [f"{n} (politician)" for n in missing]
    if retry_titles:
        retried = page_images(retry_titles)
        for n in missing:
            hit = retried.get(f"{n} (politician)")
            if hit:
                resolved[n] = hit

    # Identity check: drop matches whose page isn't a person (seals, offices, ships...).
    humans = human_qids([qid for _, _, qid in resolved.values()])
    dropped = [n for n, (_, _, qid) in resolved.items() if qid not in humans]
    resolved = {n: v for n, v in resolved.items() if v[2] in humans}
    if dropped:
        print(f"rejected {len(dropped)} non-person matches, e.g.: {dropped[:6]}")

    os.makedirs(IMG_DIR, exist_ok=True)
    photos = {}
    failures = 0
    saved_by_name = {}
    for race_id, name in candidates:
        if name not in resolved:
            continue
        thumb, title, _ = resolved[name]
        slug = slugify(race_id, name)
        path = os.path.join(IMG_DIR, f"{slug}.webp")
        try:
            # one download per unique person; copy for repeat names in other races
            if name in saved_by_name:
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
            "page": "https://en.wikipedia.org/wiki/" + urllib.parse.quote(title.replace(" ", "_")),
        }
    if failures:
        print(f"{failures} avatar downloads failed (left as fallback)")

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
