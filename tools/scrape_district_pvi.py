"""Scrape per-district 2025 Cook PVI (current 2026 lines) from the Wikipedia
'2026 United States House of Representatives elections' page wikitext.

Pairs each {{ushr|ST|N|X}} district header with the {{shading PVI|...}} template
that follows it, validates counts per state, and emits a C# dictionary body
(R-positive convention, matching DistrictElectionData.DistrictPVI).
"""
import json, re, sys

EXPECTED = {  # districts per state (2020 apportionment)
    'AL': 7, 'AK': 1, 'AZ': 9, 'AR': 4, 'CA': 52, 'CO': 8, 'CT': 5, 'DE': 1,
    'FL': 28, 'GA': 14, 'HI': 2, 'ID': 2, 'IL': 17, 'IN': 9, 'IA': 4, 'KS': 4,
    'KY': 6, 'LA': 6, 'ME': 2, 'MD': 8, 'MA': 9, 'MI': 13, 'MN': 8, 'MS': 4,
    'MO': 8, 'MT': 2, 'NE': 3, 'NV': 4, 'NH': 2, 'NJ': 12, 'NM': 3, 'NY': 26,
    'NC': 14, 'ND': 1, 'OH': 15, 'OK': 5, 'OR': 6, 'PA': 17, 'RI': 2, 'SC': 7,
    'SD': 1, 'TN': 9, 'TX': 38, 'UT': 4, 'VT': 1, 'VA': 11, 'WA': 10, 'WV': 2,
    'WI': 8, 'WY': 1,
}
STATE_NAMES = {
    'Alabama': 'AL', 'Alaska': 'AK', 'Arizona': 'AZ', 'Arkansas': 'AR',
    'California': 'CA', 'Colorado': 'CO', 'Connecticut': 'CT', 'Delaware': 'DE',
    'Florida': 'FL', 'Georgia': 'GA', 'Hawaii': 'HI', 'Idaho': 'ID',
    'Illinois': 'IL', 'Indiana': 'IN', 'Iowa': 'IA', 'Kansas': 'KS',
    'Kentucky': 'KY', 'Louisiana': 'LA', 'Maine': 'ME', 'Maryland': 'MD',
    'Massachusetts': 'MA', 'Michigan': 'MI', 'Minnesota': 'MN', 'Mississippi': 'MS',
    'Missouri': 'MO', 'Montana': 'MT', 'Nebraska': 'NE', 'Nevada': 'NV',
    'New Hampshire': 'NH', 'New Jersey': 'NJ', 'New Mexico': 'NM', 'New York': 'NY',
    'North Carolina': 'NC', 'North Dakota': 'ND', 'Ohio': 'OH', 'Oklahoma': 'OK',
    'Oregon': 'OR', 'Pennsylvania': 'PA', 'Rhode Island': 'RI', 'South Carolina': 'SC',
    'South Dakota': 'SD', 'Tennessee': 'TN', 'Texas': 'TX', 'Utah': 'UT',
    'Vermont': 'VT', 'Virginia': 'VA', 'Washington': 'WA', 'West Virginia': 'WV',
    'Wisconsin': 'WI', 'Wyoming': 'WY',
}

wt = json.load(open('house2026.json', encoding='utf-8'))['parse']['wikitext']
lines = wt.split('\n')

ushr_re = re.compile(r'\{\{ushr\|([A-Z]{2})\|(\d+|AL)\|[A-Z]\}\}')
pvi_re = re.compile(r'\{\{shading PVI\|(?:([RD])\|([\d.]+)|(EVEN))\}\}')
heading_re = re.compile(r'^(={2,4})\s*(.*?)\s*\1\s*$')

section = None            # current state abbr (only inside a state ==Section==)
pending = None            # (state, district) awaiting its PVI
newmap_flags = {}         # key -> bool (row carried the newmap footnote)
result = {}               # 'AL-01' -> signed Dem-positive pvi (we'll flip later)
order_errors = []

for line in lines:
    h = heading_re.match(line)
    if h:
        title = h.group(2).strip()
        section = STATE_NAMES.get(title)
        pending = None
        continue
    if section is None:
        continue

    m = ushr_re.search(line)
    if m and m.group(1) == section:
        dist = 1 if m.group(2) == 'AL' else int(m.group(2))
        pending = (section, dist)

    p = pvi_re.search(line)
    if p and pending:
        state, dist = pending
        key = f'{state}-{dist:02d}'
        if key in result:
            order_errors.append(f'duplicate {key}')
        if p.group(3):  # EVEN
            val = 0.0
        else:
            val = float(p.group(2)) * (1 if p.group(1) == 'D' else -1)
        result[key] = val
        newmap_flags[key] = 'newmap' in line
        pending = None

# ---- validation ----
bad = []
for st, n in EXPECTED.items():
    got = [k for k in result if k.startswith(st + '-')]
    if len(got) != n:
        bad.append(f'{st}: got {len(got)} expected {n}: {sorted(got)}')
    for d in range(1, n + 1):
        if f'{st}-{d:02d}' not in result:
            bad.append(f'missing {st}-{d:02d}')
print(f'parsed {len(result)} districts; {len(bad)} problems; {len(order_errors)} dup errors')
for b in bad[:20]:
    print('  PROBLEM:', b)

newmap_states = sorted({k[:2] for k, v in newmap_flags.items() if v})
print('states with newmap-flagged PVI rows:', newmap_states)

# spot checks against known values
spot = {'AL-01': -17, 'AL-02': -7, 'ME-02': -4, 'NV-03': 1, 'NM-02': 0}
for k, v in spot.items():
    ok = result.get(k) == v
    print(f'spot {k}: parsed {result.get(k)} expected {v} {"OK" if ok else "MISMATCH"}')

json.dump({'pvi_dem_positive': result, 'newmap': newmap_flags},
          open('pvi2026.json', 'w'), indent=0)
print('wrote pvi2026.json')
