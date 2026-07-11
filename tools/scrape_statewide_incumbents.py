"""Scrape incumbent-running status per 2026 statewide race from the Wikipedia
'2026 United States Senate elections' / 'gubernatorial elections' summary tables.

Emits raceId -> 'D' / 'R' (incumbent of that party seeking reelection) or 'O' (open:
retiring / term-limited / resigned / running for another office). This decouples
"is the incumbent running" from "has the primary produced a nominee yet" — the
model's open-seat rule needs the former, not the latter.
"""
import json, re

STATE_NAMES = {
    'Alabama': 'AL', 'Alaska': 'AK', 'Arizona': 'AZ', 'Arkansas': 'AR', 'California': 'CA',
    'Colorado': 'CO', 'Connecticut': 'CT', 'Delaware': 'DE', 'Florida': 'FL', 'Georgia': 'GA',
    'Hawaii': 'HI', 'Idaho': 'ID', 'Illinois': 'IL', 'Indiana': 'IN', 'Iowa': 'IA',
    'Kansas': 'KS', 'Kentucky': 'KY', 'Louisiana': 'LA', 'Maine': 'ME', 'Maryland': 'MD',
    'Massachusetts': 'MA', 'Michigan': 'MI', 'Minnesota': 'MN', 'Mississippi': 'MS',
    'Missouri': 'MO', 'Montana': 'MT', 'Nebraska': 'NE', 'Nevada': 'NV', 'New Hampshire': 'NH',
    'New Jersey': 'NJ', 'New Mexico': 'NM', 'New York': 'NY', 'North Carolina': 'NC',
    'North Dakota': 'ND', 'Ohio': 'OH', 'Oklahoma': 'OK', 'Oregon': 'OR', 'Pennsylvania': 'PA',
    'Rhode Island': 'RI', 'South Carolina': 'SC', 'South Dakota': 'SD', 'Tennessee': 'TN',
    'Texas': 'TX', 'Utah': 'UT', 'Vermont': 'VT', 'Virginia': 'VA', 'Washington': 'WA',
    'West Virginia': 'WV', 'Wisconsin': 'WI', 'Wyoming': 'WY',
}
OPEN_MARKERS = ['retiring', 'term-limited', 'term limited', 'resigned', 'resigning',
                'not running', 'lost renomination', 'run for', 'running for governor',
                'running for u.s. senate', 'retired']

def parse(page_json, row_re, office, section):
    wt = json.load(open(page_json, encoding='utf-8'))['parse']['wikitext']
    # Only the race-summary table has the status column; earlier tables (retirements
    # sidebar, specials) would otherwise shadow it.
    start = wt.find(section)
    assert start >= 0, f'section {section!r} not found'
    wt = wt[start:]
    out = {}
    rows = [(m.start(), m.group(1)) for m in re.finditer(row_re, wt)]
    for idx, (pos, statename) in enumerate(rows):
        st = STATE_NAMES.get(statename)
        if st is None:
            continue
        end = rows[idx + 1][0] if idx + 1 < len(rows) else min(len(wt), pos + 4000)
        block = wt[pos:end]
        # Incumbent party: the first party-shading template in the row (the incumbent cell
        # precedes the last-race and candidates cells; candidate lines use 'Party stripe').
        pm = re.search(r'\{\{[Pp]arty shading/(?:Text/)?(Republican|Democratic)\}\}', block)
        party = 'R' if pm and pm.group(1) == 'Republican' else 'D' if pm else '?'
        # Status: the text of the Hold/Gain results cell ('Incumbent retiring...',
        # 'Term-limited', 'Running'); fall back to any bare 'Incumbent ...' phrase.
        sm = re.search(r'\{\{[Pp]arty shading/(?:Hold|Gain)[^}]*\}\}[^|\n]*\|\s*([^\n]+)', block)
        status = (sm.group(1) if sm else '')
        if not status:
            sm2 = re.search(r"(Incumbent(?:'s)?[^|\n]*)", block, re.I)
            status = sm2.group(1) if sm2 else ''
        status = status.lower()
        is_open = any(k in status for k in OPEN_MARKERS)
        key = f'{st}-{office}-2026'
        # Keep the first (regular-election) row per state; specials handled by nominee flags.
        if key not in out:
            out[key] = 'O' if is_open else party
    return out

sen = parse('2026_United_States_Senate_elections.json',
            r'!\s*\[\[2026 United States Senate election in ([^|\]]+)\|', 'SEN',
            'Elections leading to the next Congress')
gov = parse('2026_United_States_gubernatorial_elections.json',
            r'!\s*\[\[2026 ([A-Za-z ]+) gubernatorial election\|', 'GOV',
            'Race summary')

print(f'senate rows: {len(sen)} | governor rows: {len(gov)}')
for k in sorted(sen): print(' ', k, sen[k])
for k in sorted(gov): print(' ', k, gov[k])
json.dump({**sen, **gov}, open('incumbents2026.json', 'w'), indent=0)
