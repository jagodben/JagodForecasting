"""Scrape real 2024 House results (Dem margin per district) from the Wikipedia
'2024 United States House of Representatives elections' page wikitext.

Walks each state section's district table: {{ushr|ST|N|X}} starts a district,
candidate bullet lines carry '{{Party stripe|<party>...}}[{{Aye}}] ... 51.9%'.
Margin = top-Dem% - top-Rep%; one-party races (unopposed / same-party top-two)
get a +/-45 safe-seat placeholder. Winner check via the {{Aye}} marker.
"""
import json, re

EXPECTED = {
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

wt = json.load(open('house2024.json', encoding='utf-8'))['parse']['wikitext']
lines = wt.split('\n')

ushr_re = re.compile(r'\{\{ushr\|([A-Z]{2})\|(\d+|AL)\|X\}\}')  # X = header form; C appears in 'redistricted from' notes
cand_re = re.compile(
    r"\{\{Party stripe\|([^}|]+?)(?: Party)?(?: \(US(?:A)?\))?\}\}\s*(\{\{[Aa]ye\}\})?\s*'*\[*\[*([^\]|(]*?)\]*\]*'*\s*\(([^)]*)\)[^%]*?([\d.]+)%",
    re.I)
heading_re = re.compile(r'^(={2,4})\s*(.*?)\s*\1\s*$')

districts = {}   # key -> list of (party, pct, won)
section = None
current = None

def flush():
    pass

for line in lines:
    h = heading_re.match(line)
    if h:
        section = STATE_NAMES.get(h.group(2).strip())
        current = None
        continue
    if section is None:
        continue
    m = ushr_re.search(line)
    if m and m.group(1) == section:
        dist = 1 if m.group(2) == 'AL' else int(m.group(2))
        current = f'{section}-{dist:02d}'
        districts.setdefault(current, [])
        continue
    if current:
        for cm in cand_re.finditer(line):
            stripe_party = cm.group(1).strip().lower()
            paren_party = cm.group(4).strip().lower()
            won = cm.group(2) is not None
            pct = float(cm.group(5))
            party = ('D' if 'democrat' in stripe_party or 'democrat' in paren_party
                     else 'R' if 'republican' in stripe_party or 'republican' in paren_party
                     else 'O')
            districts[current].append((party, pct, won))

results = {}   # key -> (dem_margin, rep_won)
problems = []
for key, cands in districts.items():
    if not cands:
        problems.append(f'{key}: no candidates parsed')
        continue
    dem = max((p for pt, p, w in cands if pt == 'D'), default=None)
    rep = max((p for pt, p, w in cands if pt == 'R'), default=None)
    winner = next(((pt, p) for pt, p, w in cands if w), None)
    if dem is not None and rep is not None:
        margin = round(dem - rep, 1)
    elif dem is not None:
        margin = 45.0     # no Republican ran (or D-vs-D top-two): safe-seat placeholder
    elif rep is not None:
        margin = -45.0
    else:
        problems.append(f'{key}: no D/R percentages: {cands}')
        continue
    rep_won = (winner[0] == 'R') if winner else (margin < 0)
    # cross-check the margin sign against the Aye winner
    if winner and winner[0] in 'DR':
        if (margin > 0) != (winner[0] == 'D') and abs(margin) > 0.05:
            problems.append(f'{key}: margin {margin} disagrees with winner {winner[0]}')
    results[key] = (margin, rep_won)

bad = []
for st, n in EXPECTED.items():
    got = [k for k in results if k.startswith(st + '-')]
    if len(got) != n:
        bad.append(f'{st}: {len(got)}/{n}')
print(f'parsed {len(results)} districts | count problems: {bad}')
print(f'row problems: {len(problems)}')
for p in problems[:15]:
    print('  ', p)

# spot checks against known real results
spot = {'AZ-01': -3.8, 'AZ-06': -2.5, 'ME-02': 0.7, 'MI-08': 6.6, 'NJ-09': 4.9, 'PA-07': -1.0, 'GA-06': 31.2, 'NY-16': 43.2, 'GA-07': -30.0}
for k, v in spot.items():
    got = results.get(k, (None,))[0]
    ok = got is not None and abs(got - v) <= 0.6
    print(f'spot {k}: parsed {got} expected ~{v} {"OK" if ok else "CHECK"}')

json.dump({k: list(v) for k, v in results.items()}, open('results2024.json', 'w'), indent=0)
print('wrote results2024.json')
