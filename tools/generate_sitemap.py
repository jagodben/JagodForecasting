"""Generates the static sitemap for jagodforecasting.com.

Writes election-forecaster-client/public/sitemap.xml with every crawlable page: the three
home views, polls, methodology, each state page, and all ~506 race pages. The race set is
fixed for the cycle, so the sitemap is generated once and committed; rerun after any change
to the race universe (there shouldn't be one before Election Day).

Set FORECAST_API to override the API the race list is pulled from (must include /api):
    FORECAST_API=https://api.jagodforecasting.com/api python tools/generate_sitemap.py
"""

import json
import os
import urllib.request

API = os.environ.get("FORECAST_API", "https://api.jagodforecasting.com/api")
SITE = "https://jagodforecasting.com"
OUT = os.path.join(os.path.dirname(__file__), "..", "election-forecaster-client", "public", "sitemap.xml")


def get(url):
    req = urllib.request.Request(url, headers={"Accept": "application/json"})
    with urllib.request.urlopen(req, timeout=60) as r:
        return json.load(r)


def main():
    urls = [
        f"{SITE}/",
        f"{SITE}/?view=house",
        f"{SITE}/?view=governors",
        f"{SITE}/polls",
        f"{SITE}/methodology",
    ]

    states = get(f"{API}/states")
    urls += [f"{SITE}/state/{s['id']}" for s in states]

    race_count = 0
    for rtype in ("Senate", "Governor", "House"):
        for race in get(f"{API}/races?type={rtype}"):
            urls.append(f"{SITE}/race/{race['id']}")
            race_count += 1

    lines = ['<?xml version="1.0" encoding="UTF-8"?>']
    lines.append('<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">')
    for url in urls:
        lines.append(f"  <url><loc>{url.replace('&', '&amp;')}</loc></url>")
    lines.append("</urlset>")

    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")

    print(f"wrote {len(urls)} urls ({len(states)} states, {race_count} races) -> {os.path.normpath(OUT)}")


if __name__ == "__main__":
    main()
