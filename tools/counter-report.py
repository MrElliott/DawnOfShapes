"""Summarise a tools/balance.ps1 output folder with the shape-counter view.

Usage: python tools/counter-report.py results/<run> [results/<run2> ...]

Prints, per folder: overall faction win rates, each matchup from the intended favourite's side
(Cubes > Spheres > Pyramids > Cubes), mean match length, counter damage share, and the equal-cost
skirmish matrix.
"""
import glob
import json
import os
import sys
from collections import defaultdict

CYCLE = [("Cubes", "Spheres"), ("Spheres", "Pyramids"), ("Pyramids", "Cubes")]


def load(folder):
    runs = []
    for path in glob.glob(os.path.join(folder, "runs", "*.json")):
        with open(path, encoding="utf-8") as f:
            runs.append(json.load(f))
    return runs


def report(folder):
    runs = load(folder)
    full = [r for r in runs if r["mode"] == "full"]
    sk = [r for r in runs if r["mode"] == "skirmish"]
    print(f"== {folder}: {len(full)} matches, {len(sk)} skirmishes")
    if full and "counterMatrix" in full[0]:
        print("counter matrix:", json.dumps(full[0]["counterMatrix"]))

    games = defaultdict(int)
    wins = defaultdict(int)
    durations = []
    net = defaultdict(float)
    dealt_counter = defaultdict(float)
    for r in full:
        a, b = r["players"][0]["faction"], r["players"][1]["faction"]
        for f in (a, b):
            games[f] += 1
        if r["winnerFaction"]:
            wins[r["winnerFaction"]] += 1
        durations.append(r["duration"])
        for p in r["players"]:
            c = p.get("counter")
            if c:
                net[p["faction"]] += c["netDamage"]
                dealt_counter[p["faction"]] += c["strongDamage"] + c["weakDamage"]

    if full:
        print("overall: " + "   ".join(f"{f} {wins[f] / games[f]:.1%} ({games[f]})" for f in sorted(games)))
        for fav, under in CYCLE:
            g = [r for r in full if {r["players"][0]["faction"], r["players"][1]["faction"]} == {fav, under}]
            if not g:
                continue
            w = sum(1 for r in g if r["winnerFaction"] == fav)
            mins = sum(r["duration"] for r in g) / len(g) / 60
            print(f"  {fav:8} > {under:8}: {w}/{len(g)} = {w / len(g):.1%}   avg {mins:.1f} min")
        print(f"mean match length: {sum(durations) / len(durations) / 60:.1f} min")
        if any(net.values()):
            print("counter net damage per match: " + "   ".join(
                f"{f} {net[f] / games[f]:+.0f}" for f in sorted(games)))

    for fmt in ("cost", "count"):
        rows = [r for r in sk if (":@" in r["armyA"]) == (fmt == "cost")]
        if not rows:
            continue
        score = defaultdict(float)
        n = defaultdict(int)
        units = set()
        for r in rows:
            ua, ub = r["armyA"].split(":")[0], r["armyB"].split(":")[0]
            units.update((ua, ub))
            n[(ua, ub)] += 1
            n[(ub, ua)] += 1
            w = r["winnerTeam"]
            score[(ua, ub)] += 1 if w == 0 else 0.5 if w not in (0, 1) else 0
            score[(ub, ua)] += 1 if w == 1 else 0.5 if w not in (0, 1) else 0
        order = [u for u in ["BlockTrooper", "Stacker", "Roller", "OrbCluster", "Spire", "PrismScout"] if u in units]
        print(f"skirmish ({fmt}) row vs column:")
        print("  " + " " * 13 + " ".join(f"{u[:6]:>6}" for u in order) + "   avg")
        for u in order:
            cells, vals = [], []
            for v in order:
                if u == v:
                    cells.append(f"{'-':>6}")
                else:
                    s = score[(u, v)] / n[(u, v)] if n[(u, v)] else float("nan")
                    vals.append(s)
                    cells.append(f"{s:6.2f}")
            print(f"  {u:13}" + " ".join(cells) + f"  {sum(vals) / len(vals):.2f}")
    print()


if __name__ == "__main__":
    for folder in sys.argv[1:]:
        report(folder)
