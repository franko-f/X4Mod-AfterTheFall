#!/usr/bin/env python3
"""L turret weapon stats, extracted from the unpacked X4 game XML.

The game version to process is discovered from the generator's own definition
(X4DataVersion in dataXml.fs), so this stays in step with whatever version the
mod is being built against. Any OTHER version directories found next to it are
compared at the turret-macro level (rotation, hull, projectile swaps) - older
unpacks predate the fx re-extract and carry no bullet data.

Outputs (into stats/processed/):
  l_turrets_report.md   plain markdown table + change list
  x4-l-turrets.html     sortable/filterable datasheet

Stat conventions (from bullet_*/missile_* macros):
- rate = reload@rate, or 1/reload@time
- magazine weapons (ammunition value V, reload R): sustained cycle = V/rate + R
- projectiles per trigger = bullet@amount * bullet@barrelamount
- damage vs hull = damage@value + damage@hull ; vs shield = damage@value + damage@shield
  (areadamage treated the same and added on top - flak/disruptor shells self-destruct)
- beams (attach=1): damage@value is applied per second while the beam is active
  (duration = lifetime); cycle = max(reload time, lifetime)
- missiles: explosiondamage@value per missile, amount missiles per launch
- range = bullet@range if present, else speed * lifetime
"""
import datetime
import glob
import json
import os
import re
import sys
import xml.etree.ElementTree as ET

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA_ROOT = os.path.join(REPO, "X4_unpacked_data")
OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "processed")

# Hand-written balance annotations, appended to the computed notes.
EDITORIAL = {
    "turret_xen_l_laser_01_mk1": "Ex-'laser', reworked + renamed in 9.0",
    "turret_xen_l_plasma_01_mk1": "Ex-'plasma', anti-shield bias",
    "turret_pir_l_battleship_01_laser_01_mk1": "Erlking battleship only (Avarice)",
    "turret_tel_l_plasma_01_mk1": "Best faction plasma",
    "turret_par_l_plasma_01_mk1": "Longest-ranged plasma",
    "turret_par_l_beam_01_mk1": "Was a beam in 8.5; longest range of all",
    "turret_spl_l_beam_01_mk1": "Was a beam in 8.5",
    "turret_bor_l_disruptor_01_mk1": "Anti-shield bias",
    "turret_bor_l_flak_01_mk1": "AoE, anti-shield / anti-fighter",
}


def game_version():
    src = open(os.path.join(REPO, "dataXml.fs")).read()
    m = re.search(r'X4DataVersion\s*=\s*"([^"]+)"', src)
    if not m:
        sys.exit("cannot find X4DataVersion in dataXml.fs")
    return m.group(1)


def fattr(el, name, default=None):
    if el is None:
        return default
    v = el.get(name)
    return float(v) if v is not None else default


def find_macro_files(root, pattern):
    hits = []
    for top in [root] + sorted(glob.glob(os.path.join(root, "extensions", "*"))):
        for dirpath, _dirnames, filenames in os.walk(os.path.join(top, "assets")):
            if os.path.basename(dirpath).lower() != "macros":
                continue
            for f in filenames:
                if re.fullmatch(pattern, f, re.I):
                    hits.append(os.path.join(dirpath, f))
    return hits


def load_texts(root):
    """(page,id) -> raw text, from all English t-files (base + extensions)."""
    texts = {}
    for tfile in glob.glob(os.path.join(root, "t", "0001-l044.xml")) + glob.glob(
        os.path.join(root, "extensions", "*", "t", "0001-l044.xml")
    ):
        try:
            tree = ET.parse(tfile)
        except ET.ParseError:
            continue
        for page in tree.getroot().iter("page"):
            pid = page.get("id")
            for t in page.iter("t"):
                if pid and t.get("id") and t.text:
                    texts[(int(pid), int(t.get("id")))] = t.text
    return texts


REF_RE = re.compile(r"\{\s*(\d+)\s*,\s*(\d+)\s*\}")


def resolve_text(texts, ref, depth=0):
    """Resolve '{page,id}' recursively, drop '(comment)' segments."""
    if depth > 8:
        return ref
    m = REF_RE.fullmatch(ref.strip())
    s = texts.get((int(m.group(1)), int(m.group(2))), ref) if m else ref
    s = re.sub(r"\((?:[^()])*\)", "", s)
    s = REF_RE.sub(lambda mm: resolve_text(texts, mm.group(0), depth + 1), s)
    return re.sub(r"\s+", " ", s).strip()


def parse_turret(path):
    mac = ET.parse(path).getroot().find("macro")
    props = mac.find("properties")
    ident = props.find("identification")
    return {
        "macro": mac.get("name"),
        "class": mac.get("class"),
        "nameref": ident.get("name") if ident is not None else None,
        "maker": ident.get("makerrace") if ident is not None else "",
        "bullet": (props.find("bullet").get("class") if props.find("bullet") is not None else None),
        "rot": fattr(props.find("rotationspeed"), "max"),
        "hull": fattr(props.find("hull"), "max"),
        "storage": fattr(props.find("storage"), "capacity"),
    }


def parse_projectile(path):
    mac = ET.parse(path).getroot().find("macro")
    props = mac.find("properties")
    b = props.find("bullet") if mac.get("class") == "bullet" else props.find("missile")
    dmg = props.find("damage")
    area = props.find("areadamage")
    expl = props.find("explosiondamage")
    ammo = props.find("ammunition")
    rel = props.find("reload")
    return {
        "class": mac.get("class"),
        "speed": fattr(b, "speed"),
        "lifetime": fattr(b, "lifetime"),
        "range": fattr(b, "range"),
        "amount": fattr(b, "amount", 1.0),
        "barrels": fattr(b, "barrelamount", 1.0),
        "attach": (b is not None and b.get("attach") == "1"),
        "guided": (b is not None and b.get("guided") == "1"),
        "dmg_base": fattr(dmg, "value", 0.0),
        "dmg_hull_x": fattr(dmg, "hull", 0.0),
        "dmg_shield_x": fattr(dmg, "shield", 0.0),
        "area_base": fattr(area, "value", 0.0),
        "area_hull_x": fattr(area, "hull", 0.0),
        "area_shield_x": fattr(area, "shield", 0.0),
        "expl_base": fattr(expl, "value", 0.0),
        "expl_shield_x": fattr(expl, "shield", 0.0),
        "ammo_val": fattr(ammo, "value"),
        "ammo_reload": fattr(ammo, "reload"),
        "rate": fattr(rel, "rate"),
        "time": fattr(rel, "time"),
    }


def race_code(macro):
    m = re.match(r"turret_(\w{3})_l_", macro)
    return (m.group(1) if m else "???").upper()


def weapon_type(name, group):
    for key, label in [
        ("Graviton", "Graviton"), ("Seismic", "Seismic"), ("Dumbfire", "Dumbfire"),
        ("Tracking", "Guided"), ("Plasma", "Plasma"), ("Proton", "Gatling"),
        ("Gatling", "Gatling"), ("Kyon", "Beam"), ("Meson", "Beam"), ("Mining", "Mining"),
        ("Flak", "Flak"), ("Railgun", "Railgun"), ("Boson", "Railgun"),
        ("Mass Driver", "Railgun"), ("Beam", "Beam"), ("Phase", "Pulse"), ("Pulse", "Pulse"),
        ("Erlking", "Battleship gun"),
    ]:
        if key.lower() in name.lower():
            return label
    return {"missile": "Missile", "beam": "Beam"}.get(group, "Gun")


def compute(t, p):
    """Derived stats for one turret."""
    is_beam = p["attach"] and p["class"] == "bullet"
    is_missile = p["class"] == "missile"
    per_trigger = (p["amount"] or 1) * (p["barrels"] or 1)
    rate = p["rate"] if p["rate"] else (1.0 / p["time"] if p["time"] else None)

    if is_missile:
        hull_hit = shield_hit = p["expl_base"]
        shield_hit += p["expl_shield_x"]
    else:
        hull_hit = p["dmg_base"] + p["dmg_hull_x"] + p["area_base"] + p["area_hull_x"]
        shield_hit = p["dmg_base"] + p["dmg_shield_x"] + p["area_base"] + p["area_shield_x"]

    rng = p["range"]
    if rng is None and p["speed"] and p["lifetime"]:
        rng = p["speed"] * p["lifetime"]

    extras = []
    if is_beam:
        dur = p["lifetime"] or 0
        cyc = max(p["time"] or 0, dur)
        burst_hull = hull_hit * (p["barrels"] or 1)  # dps while the beam is on
        sus_rate = None
        sus_hull = burst_hull * dur / cyc if cyc else 0
        sus_shield = shield_hit * (p["barrels"] or 1) * dur / cyc if cyc else 0
        rof_txt = "continuous beam" if dur >= cyc else f"{dur:g}s beam per {cyc:g}s"
        rof_sort = 1.0 / cyc if cyc else 0
        shot_hull = hull_hit * dur * (p["barrels"] or 1)
        shot_shield = shield_hit * dur * (p["barrels"] or 1)
        extras.append(f"{burst_hull:,.0f} DPS while on" if dur < cyc else "beam")
    else:
        if p["ammo_val"] and rate:
            cyc = p["ammo_val"] / rate + (p["ammo_reload"] or 0)
            sus_rate = p["ammo_val"] / cyc
            rof_txt = f"{rate:.2f} burst / {sus_rate:.2f} sust"
        else:
            sus_rate = rate
            rof_txt = f"{rate:.2f}" if rate else "?"
        rof_sort = sus_rate or 0
        burst_hull = hull_hit * per_trigger * (rate or 0)
        sus_hull = hull_hit * per_trigger * (sus_rate or 0)
        sus_shield = shield_hit * per_trigger * (sus_rate or 0)
        shot_hull = hull_hit * per_trigger
        shot_shield = shield_hit * per_trigger
        if p["speed"]:
            extras.append(f"{p['speed']:,.0f} m/s")

    if is_missile:
        extras.append("guided" if p["guided"] else "dumbfire")
        if (p["amount"] or 1) > 1:
            extras.append(f"{p['amount']:g}x cluster")
        if t["storage"]:
            extras.append(f"stores {t['storage']:g} — ammo-dependent")

    mag = f"{p['ammo_val']:g} rnd, {p['ammo_reload']:g}s" if p["ammo_val"] else "—"
    group = (
        "mining" if "mining" in t["macro"]
        else "missile" if is_missile
        else "beam" if is_beam
        else "railgun" if weapon_type(t["name"], "") == "Railgun"
        else "gun"
    )
    code = race_code(t["macro"])
    notes = ", ".join(extras)
    if t["macro"] in EDITORIAL:
        notes = f"{notes}. {EDITORIAL[t['macro']]}" if notes else EDITORIAL[t["macro"]]

    return {
        "name": t["name"], "race": code,
        "cls": {"XEN": "xen", "KHA": "kha", "PIR": "pir"}.get(code, ""),
        "type": weapon_type(t["name"], group), "group": group,
        "range": round(rng or 0), "rof": rof_txt, "rofSort": round(rof_sort, 3),
        "shotHull": round(shot_hull), "shotShield": round(shot_shield),
        "susHull": round(sus_hull or 0), "susShield": round(sus_shield or 0),
        "burstHull": round(burst_hull or 0),
        "mag": mag, "magSort": p["ammo_val"] or 0,
        "track": t["rot"] or 0, "notes": notes,
        "macro": t["macro"].removesuffix("_macro"),
    }


def collect(version, want_bullets):
    root = os.path.join(DATA_ROOT, version)
    texts = load_texts(root)
    projectiles = {}
    if want_bullets:
        for f in find_macro_files(root, r"(bullet|missile)_\w*_macro\.xml"):
            projectiles[os.path.basename(f)[: -len(".xml")]] = f
    turrets = []
    for f in sorted(set(find_macro_files(root, r"turret_\w*_l_\w*_macro\.xml"))):
        t = parse_turret(f)
        if re.search(r"_(scenario|story)_", t["macro"]):
            continue
        t["name"] = resolve_text(texts, t["nameref"]) if t["nameref"] else t["macro"]
        t["macro"] = t["macro"].removesuffix("_macro")
        turrets.append(t)
    return turrets, projectiles


def turret_level_changes(old_turrets, new_turrets, new_names):
    old = {t["macro"]: t for t in old_turrets}
    changes = []
    for t in new_turrets:
        o = old.get(t["macro"])
        label = f"{t['macro']} ({new_names.get(t['macro'], '')})".replace(" ()", "")
        if o is None:
            changes.append([label, "new in this version", "", ""])
            continue
        rot = f"{o['rot']:g} → {t['rot']:g}" if o["rot"] != t["rot"] else ""
        hull = f"{o['hull']:,.0f} → {t['hull']:,.0f}" if o["hull"] != t["hull"] else ""
        proj = ""
        if o["bullet"] != t["bullet"]:
            proj = f"{o['bullet'].removesuffix('_macro')} → {t['bullet'].removesuffix('_macro')}"
        if rot or hull or proj:
            changes.append([label, rot, hull, proj])
    for m in sorted(set(old) - {t["macro"] for t in new_turrets}):
        changes.append([m, "removed in this version", "", ""])
    return changes


def write_markdown(path, rows, changes, version, other_versions):
    with open(path, "w") as f:
        f.write(f"# X4 {version} — L turret weapons\n\n")
        f.write("| Weapon | Maker | Type | Range m | Rate of fire | Dmg/shot hull | Dmg/shot shield "
                "| Sust. DPS hull | Sust. DPS shield | Burst DPS hull | Magazine | Track °/s | Notes |\n")
        f.write("|---|---|---|---|---|---|---|---|---|---|---|---|---|\n")
        for r in rows:
            f.write(f"| {r['name']} | {r['race']} | {r['type']} | {r['range']:,} | {r['rof']} "
                    f"| {r['shotHull']:,} | {r['shotShield']:,} | {r['susHull']:,} | {r['susShield']:,} "
                    f"| {r['burstHull']:,} | {r['mag']} | {r['track']:g} | {r['notes']} |\n")
        for ver, chg in zip(other_versions, changes):
            f.write(f"\n## {ver} → {version} changes (turret-macro level)\n\n")
            if not chg:
                f.write("No turret-macro level changes.\n")
                continue
            f.write("| Turret | Rotation °/s | Turret hull | Projectile change |\n|---|---|---|---|\n")
            for c in chg:
                f.write(f"| {c[0]} | {c[1] or '—'} | {c[2] or '—'} | {c[3] or '—'} |\n")
        f.write("\n" + METHOD_NOTES_MD)


METHOD_NOTES_MD = """## How the numbers are derived

From `bullet_*` / `missile_*` macros:

- Damage per shot = `damage@value` (+ `@hull`/`@shield` specifics; `areadamage` added for
  flak/disruptor) × projectiles per trigger (`amount × barrelamount`).
- Rate of fire = `reload@rate`, or 1/`reload@time`. Magazine weapons: sustained cycle =
  mag ÷ rate + magazine reload.
- Range = `bullet@range` where present, else projectile speed × lifetime.
- Beams: `damage@value` treated as damage per second while the beam is active (`lifetime`),
  cycled by reload — relative ordering is robust; verify absolute beam DPS in-game.
- Missile turrets fire their default missile; per-shot figure is one full launch.
  Stations must be resupplied with missile ammunition.
- Excluded: scenario/story variants.
"""


def build_tiles(rows):
    guns = [r for r in rows if r["group"] == "gun" and not r["cls"]]
    top = max(rows, key=lambda r: r["susHull"])
    top_gun = max(guns, key=lambda r: r["susHull"])
    runner = max((r for r in guns if r is not top_gun), key=lambda r: r["susHull"])
    longest = max(rows, key=lambda r: r["range"])
    missiles = [r for r in rows if r["group"] == "missile"]
    tiles = [
        ("xen" if top["cls"] == "xen" else "", "Top sustained DPS", f"{top['susHull']:,}",
         f"{top['name']} — {top['susHull'] / top_gun['susHull']:.1f}× the best faction gun"),
        ("", "Best faction gun", f"{top_gun['susHull']:,}",
         f"{top_gun['name']} ({runner['name']}: {runner['susHull']:,})"),
        ("", "Longest range", f"{longest['range'] / 1000:.1f} km", longest["name"]),
    ]
    if missiles:
        top_m = max(missiles, key=lambda r: r["susHull"])
        tiles.append(("", "Top ammo-fed DPS", f"{top_m['susHull']:,}",
                      f"{top_m['name']} — needs missile resupply"))
    return tiles


def write_html(path, rows, changes, version, other_versions):
    tiles_html = "\n".join(
        f'<div class="tile {c}"><div class="k">{k}</div><div class="v">{v}</div>'
        f'<div class="d">{d}</div></div>'
        for c, k, v, d in build_tiles(rows)
    )
    chg_note = (
        f"The {', '.join(other_versions)} unpack(s) carry no bullet data (fx was excluded when they "
        f"were extracted), so this covers what the older XML does record: turret rotation, turret "
        f"hull, and which projectile each turret fires."
        if other_versions else "No other game version directories found to compare against."
    )
    chg_sections = "\n".join(
        f'<h2>{ver} → {version} changes (turret-macro level)</h2>\n'
        f'<p class="sectionnote">{chg_note}</p>\n'
        f'<div class="tablebox changesbox"><table data-changes="{ver}">'
        f'<thead><tr><th class="txt">Turret macro</th><th class="txt">Rotation °/s</th>'
        f'<th class="txt">Turret hull</th><th class="txt">Projectile change</th></tr></thead>'
        f'<tbody></tbody></table></div>'
        for ver in other_versions
    )
    stamp = datetime.date.today().isoformat()
    html = (
        HTML_TEMPLATE
        .replace("__VERSION__", version)
        .replace("__STAMP__", stamp)
        .replace("__COUNT__", str(len(rows)))
        .replace("__TILES__", tiles_html)
        .replace("__CHANGE_SECTIONS__", chg_sections)
        .replace("__ROWS_JSON__", json.dumps(rows))
        .replace("__CHANGES_JSON__", json.dumps(dict(zip(other_versions, changes))))
    )
    open(path, "w").write(html)


HTML_TEMPLATE = r"""<title>X4 __VERSION__ — L Turret Datasheet</title>
<style>
  :root {
    --bg: #F4F6F5; --surface: #FFFFFF; --ink: #1B2427; --muted: #5C6E72;
    --line: #D9E0DE; --line-soft: #E7ECEA; --accent: #0F7C86;
    --accent-soft: rgba(15, 124, 134, 0.13); --xenon: #B23B2E; --khaak: #7A4FA0;
    --pirate: #B07A1F; --shield: #2F6FB0; --bar-track: #EAEEEC; --tile-num: #10343A;
  }
  @media (prefers-color-scheme: dark) {
    :root {
      --bg: #131719; --surface: #1B2124; --ink: #DCE3E3; --muted: #90A2A5;
      --line: #2A3336; --line-soft: #222B2E; --accent: #45BDC6;
      --accent-soft: rgba(69, 189, 198, 0.14); --xenon: #E2604F; --khaak: #AF84D6;
      --pirate: #D19A3D; --shield: #6FA8DC; --bar-track: #232B2E; --tile-num: #CBE7EA;
    }
  }
  :root[data-theme="dark"] {
    --bg: #131719; --surface: #1B2124; --ink: #DCE3E3; --muted: #90A2A5;
    --line: #2A3336; --line-soft: #222B2E; --accent: #45BDC6;
    --accent-soft: rgba(69, 189, 198, 0.14); --xenon: #E2604F; --khaak: #AF84D6;
    --pirate: #D19A3D; --shield: #6FA8DC; --bar-track: #232B2E; --tile-num: #CBE7EA;
  }
  :root[data-theme="light"] {
    --bg: #F4F6F5; --surface: #FFFFFF; --ink: #1B2427; --muted: #5C6E72;
    --line: #D9E0DE; --line-soft: #E7ECEA; --accent: #0F7C86;
    --accent-soft: rgba(15, 124, 134, 0.13); --xenon: #B23B2E; --khaak: #7A4FA0;
    --pirate: #B07A1F; --shield: #2F6FB0; --bar-track: #EAEEEC; --tile-num: #10343A;
  }
  * { box-sizing: border-box; }
  body { margin: 0; background: var(--bg); color: var(--ink);
    font-family: "Avenir Next", "Segoe UI", system-ui, sans-serif; font-size: 14px; line-height: 1.5; }
  .wrap { max-width: 1600px; margin: 0 auto; padding: 28px 28px 64px; }
  .eyebrow { font-family: "Avenir Next Condensed", "Arial Narrow", "Avenir Next", sans-serif;
    font-weight: 600; text-transform: uppercase; letter-spacing: 0.14em; font-size: 12px;
    color: var(--accent); margin: 0 0 2px; }
  h1 { font-family: "Avenir Next Condensed", "Arial Narrow", "Avenir Next", sans-serif;
    font-weight: 600; font-size: 34px; letter-spacing: 0.01em; margin: 0 0 4px; text-wrap: balance; }
  .sub { color: var(--muted); margin: 0; max-width: 72ch; }
  .tiles { display: flex; flex-wrap: wrap; gap: 12px; margin: 22px 0 18px; }
  .tile { flex: 1 1 220px; background: var(--surface); border: 1px solid var(--line);
    border-radius: 6px; padding: 12px 16px 10px; }
  .tile .k { font-family: "Avenir Next Condensed", "Arial Narrow", sans-serif; font-weight: 600;
    text-transform: uppercase; letter-spacing: 0.1em; font-size: 11px; color: var(--muted); }
  .tile .v { font-family: ui-monospace, "SF Mono", Menlo, monospace; font-size: 22px;
    font-weight: 600; color: var(--tile-num); font-variant-numeric: tabular-nums; }
  .tile .d { font-size: 12.5px; color: var(--muted); }
  .tile.xen .v { color: var(--xenon); }
  .controls { display: flex; flex-wrap: wrap; gap: 10px; align-items: center; margin: 0 0 12px; }
  .controls input[type="search"] { background: var(--surface); border: 1px solid var(--line);
    border-radius: 6px; color: var(--ink); font: inherit; padding: 7px 12px; width: 260px; }
  .controls input[type="search"]:focus { outline: 2px solid var(--accent); outline-offset: 1px; }
  .chip { background: var(--surface); border: 1px solid var(--line); border-radius: 999px;
    color: var(--muted); font: inherit; font-size: 13px; padding: 5px 14px; cursor: pointer; }
  .chip[aria-pressed="true"] { background: var(--accent-soft); border-color: var(--accent);
    color: var(--accent); font-weight: 600; }
  .chip:focus-visible { outline: 2px solid var(--accent); outline-offset: 1px; }
  .hint { color: var(--muted); font-size: 12.5px; margin-left: auto; }
  .tablebox { background: var(--surface); border: 1px solid var(--line); border-radius: 6px;
    overflow-x: auto; }
  table { border-collapse: collapse; width: 100%; min-width: 1380px; }
  thead th { position: sticky; top: 0; background: var(--surface);
    border-bottom: 2px solid var(--line);
    font-family: "Avenir Next Condensed", "Arial Narrow", sans-serif; font-weight: 600;
    text-transform: uppercase; letter-spacing: 0.07em; font-size: 11.5px; color: var(--muted);
    text-align: right; padding: 10px 12px 8px; white-space: nowrap; cursor: pointer;
    user-select: none; }
  thead th.txt { text-align: left; }
  thead th:hover { color: var(--accent); }
  thead th .arrow { color: var(--accent); font-size: 10px; }
  tbody td { border-bottom: 1px solid var(--line-soft); padding: 7px 12px; text-align: right;
    white-space: nowrap; font-family: ui-monospace, "SF Mono", Menlo, monospace;
    font-size: 12.5px; font-variant-numeric: tabular-nums; }
  tbody tr:hover { background: var(--accent-soft); }
  tbody tr:last-child td { border-bottom: none; }
  td.txt { text-align: left; font-family: "Avenir Next", "Segoe UI", system-ui, sans-serif;
    font-size: 13.5px; }
  td.name { font-weight: 600; min-width: 220px; white-space: normal; }
  td.notes { color: var(--muted); font-size: 12.5px; min-width: 210px; white-space: normal; }
  .race { display: inline-block; font-family: ui-monospace, "SF Mono", Menlo, monospace;
    font-size: 11px; font-weight: 700; letter-spacing: 0.06em; border-radius: 4px;
    padding: 1.5px 7px; border: 1px solid var(--line); color: var(--muted); }
  .race.xen { color: var(--xenon); border-color: var(--xenon); }
  .race.kha { color: var(--khaak); border-color: var(--khaak); }
  .race.pir { color: var(--pirate); border-color: var(--pirate); }
  .dpscell { display: flex; align-items: center; justify-content: flex-end; gap: 10px; }
  .bar { width: 110px; height: 7px; border-radius: 4px; background: var(--bar-track);
    overflow: hidden; flex: none; }
  .bar i { display: block; height: 100%; border-radius: 4px; background: var(--accent); }
  tr.xen .bar i { background: var(--xenon); }
  tr.kha .bar i { background: var(--khaak); }
  tr.pir .bar i { background: var(--pirate); }
  .shieldnum { color: var(--shield); }
  h2 { font-family: "Avenir Next Condensed", "Arial Narrow", sans-serif; font-weight: 600;
    font-size: 22px; margin: 40px 0 6px; }
  .sectionnote { color: var(--muted); margin: 0 0 14px; max-width: 88ch; }
  .changesbox table { min-width: 900px; }
  .changesbox td.txt { font-family: ui-monospace, "SF Mono", Menlo, monospace; font-size: 12.5px; }
  .up { color: var(--accent); }
  .down { color: var(--xenon); }
  .foot { margin-top: 28px; border-top: 1px solid var(--line); padding-top: 14px;
    color: var(--muted); font-size: 13px; max-width: 100ch; }
  .foot ul { margin: 6px 0 0; padding-left: 20px; }
  .foot li { margin-bottom: 3px; }
  code { font-family: ui-monospace, "SF Mono", Menlo, monospace; font-size: 0.92em;
    background: var(--accent-soft); border-radius: 3px; padding: 0 4px; }
  @media (prefers-reduced-motion: no-preference) { .bar i { transition: width 0.4s ease; } }
</style>

<div class="wrap">
  <p class="eyebrow">X4 Foundations · __VERSION__ unpacked XML · __STAMP__</p>
  <h1>Large Turret Weapons Datasheet</h1>
  <p class="sub">All __COUNT__ equippable L turrets from the __VERSION__ game data (base + DLC),
  with sustained and burst DPS derived from their bullet and missile macros. Click any column
  header to sort; bars are scaled to the highest sustained hull DPS.</p>

  <div class="tiles">
__TILES__
  </div>

  <div class="controls" role="group" aria-label="Filters">
    <input id="search" type="search" placeholder="Filter: name, race, type, notes…" aria-label="Filter rows">
    <button class="chip" data-group="all" aria-pressed="true">All</button>
    <button class="chip" data-group="gun" aria-pressed="false">Guns</button>
    <button class="chip" data-group="beam" aria-pressed="false">Beams</button>
    <button class="chip" data-group="missile" aria-pressed="false">Missiles</button>
    <button class="chip" data-group="railgun" aria-pressed="false">Railguns</button>
    <button class="chip" data-group="mining" aria-pressed="false">Mining</button>
    <span class="hint" id="count"></span>
  </div>

  <div class="tablebox">
    <table id="tbl">
      <thead><tr>
        <th class="txt" data-k="name" data-t="s">Weapon</th>
        <th class="txt" data-k="race" data-t="s">Maker</th>
        <th class="txt" data-k="type" data-t="s">Type</th>
        <th data-k="range" data-t="n">Range m</th>
        <th class="txt" data-k="rofSort" data-t="n">Rate of fire</th>
        <th data-k="shotHull" data-t="n">Dmg/shot hull</th>
        <th data-k="shotShield" data-t="n">Dmg/shot shield</th>
        <th data-k="susHull" data-t="n">Sust. DPS hull</th>
        <th data-k="susShield" data-t="n">Sust. DPS shield</th>
        <th data-k="burstHull" data-t="n">Burst DPS hull</th>
        <th class="txt" data-k="magSort" data-t="n">Magazine</th>
        <th data-k="track" data-t="n">Track °/s</th>
        <th class="txt" data-k="notes" data-t="s">Notes</th>
      </tr></thead>
      <tbody id="tb"></tbody>
    </table>
  </div>

__CHANGE_SECTIONS__

  <div class="foot">
    <strong>How the numbers are derived</strong> (from <code>bullet_*</code> / <code>missile_*</code> macros):
    <ul>
      <li>Damage per shot = <code>damage@value</code> (+ <code>@hull</code>/<code>@shield</code> specifics; <code>areadamage</code> added for flak/disruptor) × projectiles per trigger (<code>amount × barrelamount</code>).</li>
      <li>Rate of fire = <code>reload@rate</code>, or 1/<code>reload@time</code>. Magazine weapons: sustained cycle = mag ÷ rate + magazine reload.</li>
      <li>Range = <code>bullet@range</code> where present, else projectile speed × lifetime.</li>
      <li>Beams: <code>damage@value</code> treated as damage per second while the beam is active (<code>lifetime</code>), cycled by reload — relative ordering is robust; verify absolute beam DPS in-game.</li>
      <li>Missile turrets fire their default missile; per-shot figure is one full launch. Stations must be resupplied with missile ammunition.</li>
      <li>Excluded: scenario/story variants. Generated by <code>stats/l_turret_stats.py</code>.</li>
    </ul>
  </div>
</div>

<script>
const rows = __ROWS_JSON__;
const changesByVersion = __CHANGES_JSON__;

const maxDps = Math.max(...rows.map(r => r.susHull));
const fmt = n => n.toLocaleString("en-US");
let sortKey = "susHull", sortDir = -1, group = "all", query = "";

function render() {
  const t = (a, b) => typeof a === "string" ? a.localeCompare(b) : a - b;
  const visible = rows
    .filter(r => group === "all" || r.group === group)
    .filter(r => !query || (r.name + " " + r.race + " " + r.type + " " + r.notes + " " + r.macro).toLowerCase().includes(query))
    .sort((a, b) => sortDir * t(a[sortKey], b[sortKey]));
  document.getElementById("tb").innerHTML = visible.map(r => `
    <tr class="${r.cls}" title="${r.macro}_macro">
      <td class="txt name">${r.name}</td>
      <td class="txt"><span class="race ${r.cls}">${r.race}</span></td>
      <td class="txt">${r.type}</td>
      <td>${fmt(r.range)}</td>
      <td class="txt">${r.rof}</td>
      <td>${fmt(r.shotHull)}</td>
      <td class="shieldnum">${fmt(r.shotShield)}</td>
      <td><div class="dpscell"><div class="bar"><i style="width:${(100 * r.susHull / maxDps).toFixed(1)}%"></i></div><span>${fmt(r.susHull)}</span></div></td>
      <td class="shieldnum">${fmt(r.susShield)}</td>
      <td>${fmt(r.burstHull)}</td>
      <td class="txt">${r.mag}</td>
      <td>${r.track}</td>
      <td class="txt notes">${r.notes}</td>
    </tr>`).join("");
  document.getElementById("count").textContent = visible.length + " of " + rows.length + " turrets";
  document.querySelectorAll("#tbl thead th").forEach(th => {
    const base = th.textContent.replace(/ [▲▼]$/, "");
    th.innerHTML = th.dataset.k === sortKey
      ? base + ' <span class="arrow">' + (sortDir < 0 ? "▼" : "▲") + "</span>"
      : base;
  });
}

document.querySelectorAll("#tbl thead th").forEach(th => {
  th.addEventListener("click", () => {
    const k = th.dataset.k;
    if (sortKey === k) sortDir = -sortDir;
    else { sortKey = k; sortDir = th.dataset.t === "n" ? -1 : 1; }
    render();
  });
});
document.querySelectorAll(".chip").forEach(c => {
  c.addEventListener("click", () => {
    group = c.dataset.group;
    document.querySelectorAll(".chip").forEach(x => x.setAttribute("aria-pressed", x === c ? "true" : "false"));
    render();
  });
});
document.getElementById("search").addEventListener("input", e => {
  query = e.target.value.trim().toLowerCase();
  render();
});

document.querySelectorAll("table[data-changes]").forEach(tbl => {
  const chg = changesByVersion[tbl.dataset.changes] || [];
  const cell = s => {
    if (!s) return '<td class="txt">—</td>';
    const m = s.match(/([\d,.]+) → ([\d,.]+)/);
    if (m) {
      const up = parseFloat(m[2].replace(/,/g, "")) > parseFloat(m[1].replace(/,/g, ""));
      return `<td class="txt"><span class="${up ? "up" : "down"}">${s}</span></td>`;
    }
    return `<td class="txt">${s}</td>`;
  };
  tbl.querySelector("tbody").innerHTML = chg.map(c =>
    `<tr><td class="txt">${c[0]}</td>${cell(c[1])}${cell(c[2])}${cell(c[3])}</tr>`
  ).join("");
});

render();
</script>
"""


def main():
    version = game_version()
    if not os.path.isdir(os.path.join(DATA_ROOT, version)):
        sys.exit(f"X4DataVersion is {version} but {DATA_ROOT}/{version} does not exist")
    other_versions = sorted(
        d for d in os.listdir(DATA_ROOT)
        if d != version and os.path.isdir(os.path.join(DATA_ROOT, d))
    )
    print(f"processing X4 {version} (comparing against: {', '.join(other_versions) or 'nothing'})")

    turrets, projectiles = collect(version, want_bullets=True)
    names = {t["macro"]: t["name"] for t in turrets}
    rows = []
    for t in turrets:
        pfile = projectiles.get(t["bullet"])
        if not pfile:
            print(f"WARN no projectile macro for {t['macro']} -> {t['bullet']}", file=sys.stderr)
            continue
        rows.append(compute(t, parse_projectile(pfile)))
    rows.sort(key=lambda r: -r["susHull"])

    changes = []
    for ver in other_versions:
        old_turrets, _ = collect(ver, want_bullets=False)
        changes.append(turret_level_changes(old_turrets, turrets, names))

    os.makedirs(OUT_DIR, exist_ok=True)
    md = os.path.join(OUT_DIR, "l_turrets_report.md")
    html = os.path.join(OUT_DIR, "x4-l-turrets.html")
    write_markdown(md, rows, changes, version, other_versions)
    write_html(html, rows, changes, version, other_versions)
    print(f"wrote {md}\nwrote {html}\n{len(rows)} turrets")


if __name__ == "__main__":
    main()
