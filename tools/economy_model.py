#!/usr/bin/env python3
"""Long-run economy model (update 3.1): spending, coin adverts and checkpoint balances on top of real-game traces.

The traces come from the real game code (tools/Harness/Sim, mode economy-trace): every coin credit and every power-up an
achievement grants, day by day, with nothing spent. This script adds a simple player on top: a power-up is wanted on
some of the levels that took three or more attempts and on every Nth level; owned power-ups are used first, then one is
bought (a three-pack for the dedicated player in 3.1), using the not-enough-coins advert when it covers the gap; a want
that can't be paid for is counted as unmet, with the coins it lacked. Coins-tab adverts watched per day are part of the
profile. Version 2 = the economy before update 3.1, version 3 = update 3.1.

Make the traces (about 20 seconds each), then run the model:
  cd tools/Harness && dotnet build Sim -c Release
  for v in 2 3; do
    dotnet Sim/bin/Release/net8.0/Sim.dll economy-trace Casual 2 4 1 Mixed 520 $v > OUT/casual_v$v.txt
    dotnet Sim/bin/Release/net8.0/Sim.dll economy-trace Engaged 4 5 2 Mixed 520 $v > OUT/engaged_v$v.txt
    dotnet Sim/bin/Release/net8.0/Sim.dll economy-trace Dedicated 10 7 7 Planner 520 $v > OUT/dedicated_v$v.txt
  done
  python3 tools/economy_model.py OUT
"""
import collections
import os
import sys

D = ''
PRICE = {'Swap': 75, 'Blast': 100, 'TileBreaker': 125, 'ColourShift': 150, 'ColourBurst': 250}
PACK = {'Swap': 200, 'Blast': 270, 'TileBreaker': 340, 'ColourShift': 400, 'ColourBurst': 675}
UNLOCK = {'Blast': 10, 'TileBreaker': 20, 'Swap': 30, 'ColourShift': 40, 'ColourBurst': 60}
ORDER = ['Blast', 'TileBreaker', 'Swap', 'ColourShift', 'ColourBurst']
# Coin adverts per version: Coins tab reward, shared daily allowance (None = each placement its own), per-placement limits, shortfall cap
ADS = {2: dict(shop=50, shared=None, shop_max=5, short_max=5, cap=50), 3: dict(shop=20, shared=3, shop_max=3, short_max=3, cap=50)}
CPS = (25, 100, 200, 326, 500)

def load(f):
    lv, cr, gr, days = [], collections.defaultdict(list), collections.defaultdict(list), {}
    for line in open(D + f):
        x = line.strip().split(',')
        if x[0] == 'lvl': lv.append((int(x[2]), int(x[3]), int(x[4]), int(x[5]), int(x[6])))
        elif x[0] == 'credit': cr[int(x[2])].append((x[3], int(x[4])))
        elif x[0] == 'grant': gr[int(x[2])].append((x[4], int(x[5])))
        elif x[0] == 'day': days[int(x[2])] = int(x[3])
    return lv, cr, gr, days

def src(reason):
    p = reason.split('.')
    if p[0] == 'c': return 'campaign'
    return {'d': 'daily', 'w': 'weekly', 'e': 'endless', 'ach': 'achievements'}[p[0]]

def run(trace, version, stuck_share=2, extra_every=15, shop_ads=0, packs=False):
    lv, cr, gr, days = load(trace)
    ads = ADS[version]
    by_day = collections.defaultdict(list)
    for L, d, att, won, st in lv: by_day[d].append((L, att))
    bal, inv = 0, collections.Counter()
    inc = collections.Counter(); pu_ach = 0
    wants = used = bought = unmet = unmet_coins = packs_bought = 0
    stuck_seen = 0; rot = 0
    afford = collections.Counter(); afford_pack = collections.Counter(); afford_days = collections.Counter()
    out = {}; level = 0
    phase_inc = collections.Counter(); phase_days = collections.Counter()
    for d in range(1, max(days) + 1):
        used_ads = 0; shop_used = 0; short_used = 0
        def can_ad(kind):
            if ads['shared'] is not None and used_ads >= ads['shared']: return False
            return (shop_used < ads['shop_max']) if kind == 'shop' else (short_used < ads['short_max'])
        # Coins tab adverts the player chooses to watch today (before playing)
        for _ in range(shop_ads):
            if not can_ad('shop'): break
            bal += ads['shop']; inc['ads'] += ads['shop']; used_ads += 1; shop_used += 1
        for L, att in by_day[d]:
            level = L
            stuck = att >= 3
            if stuck:
                stuck_seen += 1
                stuck = stuck_seen % stuck_share == 0
            want = int(stuck) + (L >= 10 and L % extra_every == 0)
            unlocked = [k for k in ORDER if L >= UNLOCK[k]]
            if not unlocked: want = 0
            for _ in range(want):
                wants += 1
                kind = unlocked[rot % len(unlocked)]; rot += 1
                have = [k for k in unlocked if inv[k] > 0]
                if inv[kind] > 0: inv[kind] -= 1; used += 1; continue
                if have: inv[have[0]] -= 1; used += 1; continue
                price = PRICE[kind]
                if packs and bal >= PACK[kind] + 150:
                    bal -= PACK[kind]; inv[kind] += 2; used += 1; bought += 1; packs_bought += 1; continue
                if bal < price and price - bal <= ads['cap'] and can_ad('short'):
                    gap = price - bal; bal += gap; inc['ads'] += gap; used_ads += 1; short_used += 1
                if bal >= price: bal -= price; bought += 1; used += 1
                else: unmet += 1; unmet_coins += price - bal
        for r, a in cr[d]:
            bal += a; inc[src(r)] += a
            ph = 0 if level <= 100 else 1 if level <= 326 else 2
            if src(r) != 'achievements': phase_inc[ph] += a
        ph = 0 if level <= 100 else 1 if level <= 326 else 2
        phase_days[ph] += 1
        for k, n in gr[d]: inv[k] += n; pu_ach += n
        for k in ORDER:
            if days[d] >= UNLOCK[k]:
                afford_days[k] += 1
                if bal >= PRICE[k]: afford[k] += 1
                if bal >= PACK[k]: afford_pack[k] += 1
        for cp in CPS:
            if cp not in out and days[d] >= cp:
                out[cp] = dict(day=d, earned=sum(inc.values()), inc=dict(inc), balance=bal, inv=sum(inv.values()), pu_ach=pu_ach,
                               wants=wants, used=used, bought=bought, unmet=unmet, unmet_coins=unmet_coins, packs=packs_bought)
    out['afford'] = {k: round(100 * afford[k] / max(1, afford_days[k])) for k in ORDER}
    out['afford_pack'] = {k: round(100 * afford_pack[k] / max(1, afford_days[k])) for k in ORDER}
    out['daily'] = {p: round(phase_inc[p] / max(1, phase_days[p]), 1) for p in range(3)}
    out['days'] = max(days)
    return out

PROFILES = {
    # name: trace, spending behaviour, Coins-tab adverts watched per day
    'Casual':     ('casual', dict(stuck_share=2, extra_every=20, shop_ads=0)),
    'Engaged':    ('engaged', dict(stuck_share=2, extra_every=15, shop_ads=1)),
    'Dedicated':  ('dedicated', dict(stuck_share=1, extra_every=10, shop_ads=9)),
    'Struggling': ('casual', dict(stuck_share=1, extra_every=10, shop_ads=0)),
}
if __name__ == '__main__':
    D = os.path.join(sys.argv[1] if len(sys.argv) > 1 else '.', '')
    res = {}
    for name, (trace, kw) in PROFILES.items():
        for v in (2, 3):
            k = dict(kw)
            if name == 'Dedicated' and v == 3: k['packs'] = True
            res[f'{name}_v{v}'] = run(f'{trace}_v{v}.txt', v, **k)
    for name in PROFILES:
        print(f'=== {name}')
        for v in (2, 3):
            r = res[f'{name}_v{v}']
            print(f'  v{v} ({"before 3.1" if v == 2 else "3.1"})  days {r["days"]}  daily play income L1-100 {r["daily"][0]}  L101-326 {r["daily"][1]}  L327-500 {r["daily"][2]}')
            for cp in CPS:
                c = r[cp]
                print(f'    L{cp:3} day {c["day"]:3}  earned {c["earned"]:6} (camp {c["inc"].get("campaign",0):5} ach {c["inc"].get("achievements",0):5} ads {c["inc"].get("ads",0):5} daily {c["inc"].get("daily",0):4} wk {c["inc"].get("weekly",0):4} end {c["inc"].get("endless",0):3})'
                      f'  bal {c["balance"]:6}  inv {c["inv"]:3}  achPU {c["pu_ach"]:3}  wants {c["wants"]:3} used {c["used"]:3} bought {c["bought"]:3} packs {c["packs"]:2} unmet {c["unmet"]:3} gap {c["unmet_coins"]}')
            print('    affordable (% of days after unlock, single / pack):', ' '.join(f'{k} {r["afford"][k]}/{r["afford_pack"][k]}' for k in ORDER))

    print('=== Struggling player (casual trace, help wanted on every stuck level and every 10th): sensitivity to adverts')
    for label, kw in [('no Coins-tab adverts', dict(shop_ads=0)), ('1 advert a day', dict(shop_ads=1)), ('3 adverts a day', dict(shop_ads=3)),
                      ('no adverts, help every 2nd stuck level', dict(shop_ads=0, stuck_share=2))]:
        for v in (2, 3):
            k = dict(stuck_share=1, extra_every=10)
            k.update(kw)
            r = run(f'casual_v{v}.txt', v, **k)
            a, b = r[200], r[500]
            print(f'  v{v} {label:40} L200 bal {a["balance"]:5} unmet {a["unmet"]:2}/{a["wants"]:3} | L500 bal {b["balance"]:5} unmet {b["unmet"]:2}/{b["wants"]:3}'
                  f' short {b["unmet_coins"]:5} coins | ads {b["inc"].get("ads", 0):5} | Colour Burst affordable on {r["afford"]["ColourBurst"]}% of days')
