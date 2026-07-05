#!/usr/bin/env python3
"""Extract a sorted, human-readable summary of the mod's decisions from the
generated output. Compared against the committed world_summary.golden by
verify.sh - a deliberate balance change shows up here as a small reviewable
diff (and the golden is then updated with --update in verify.sh).

The summary is order-insensitive by construction (every section is sorted),
so it is immune to refactors that only rearrange output.

Usage: world_summary.py [MOD_DIR]   (writes the summary to stdout)
"""

import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import x4xml

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

_STATION_MOVE_RE = re.compile(r"^//god/stations/station\[@id='([^']+)'\]/location/@(class|macro)$")
_STATION_REMOVE_RE = re.compile(r"^//god/stations/station\[@id='([^']+)'\]$")
_PRODUCT_QUOTA_RE = re.compile(r"^//god/products/product\[@id='([^']+)'\]/quotas/quota/@(\w+)$")
_JOB_QUOTA_RE = re.compile(r"^//jobs/job\[@id='([^']+)'\]/quota$")
_JOB_ENV_RE = re.compile(r"^//jobs/job\[@id='([^']+)'\]/environment$")


def _fmt_attrs(el, keys):
    return " ".join(f"{k}={el.get(k)}" for k in keys if el.get(k) is not None)


def summarise_god(root, lines):
    moves = {}
    for op in root:
        if not x4xml.is_element(op):
            continue
        sel = op.get("sel", "")

        if op.tag == "add" and sel == "//god/stations":
            for st in op:
                if not x4xml.is_element(st):
                    continue
                loc = st.find("location")
                spec = st.find("station")
                plan = spec.get("constructionplan") if spec is not None else None
                where = f"{loc.get('class')}:{loc.get('macro')}" if loc is not None else "?"
                extra = f" plan={plan}" if plan else ""
                lines.append(f"station-add {st.get('id')} owner={st.get('owner')} type={st.get('type')} @ {where}{extra}")
        elif op.tag == "add" and sel == "//god/products":
            for pr in op:
                if not x4xml.is_element(pr):
                    continue
                loc = pr.find("location")
                where = f"{loc.get('class')}:{loc.get('macro')}" if loc is not None else "?"
                quota = pr.find("quotas/quota")
                q = _fmt_attrs(quota, ["galaxy", "sector", "cluster"]) if quota is not None else ""
                lines.append(f"product-add {pr.get('id')} ware={pr.get('ware')} owner={pr.get('owner')} @ {where} {q}")
        elif op.tag == "remove" and _STATION_REMOVE_RE.match(sel):
            lines.append(f"station-remove {_STATION_REMOVE_RE.match(sel).group(1)}")
        elif op.tag == "replace" and _STATION_MOVE_RE.match(sel):
            m = _STATION_MOVE_RE.match(sel)
            moves.setdefault(m.group(1), {})[m.group(2)] = (op.text or "").strip()
        elif op.tag == "replace" and _PRODUCT_QUOTA_RE.match(sel):
            m = _PRODUCT_QUOTA_RE.match(sel)
            lines.append(f"product-quota {m.group(1)} {m.group(2)}={(op.text or '').strip()}")
        else:
            lines.append(f"other-op {op.tag} {sel}")

    for station_id, parts in moves.items():
        lines.append(f"station-move {station_id} -> {parts.get('class', '?')}:{parts.get('macro', '?')}")


def summarise_jobs(root, lines):
    for op in root:
        if not x4xml.is_element(op):
            continue
        sel = op.get("sel", "")
        if op.tag == "add" and sel == "/jobs":
            for job in op:
                if x4xml.is_element(job):
                    lines.append(f"job-add {job.get('id')}")
        elif op.tag == "replace" and _JOB_QUOTA_RE.match(sel):
            quota = op.find("quota")
            attrs = _fmt_attrs(quota, ["galaxy", "maxgalaxy", "cluster", "sector"]) if quota is not None else "?"
            lines.append(f"job-quota {_JOB_QUOTA_RE.match(sel).group(1)} {attrs}")
        elif op.tag in ("replace", "add") and _JOB_ENV_RE.match(sel):
            env = op.find("environment")
            attrs = _fmt_attrs(env, ["preferbuilding", "buildatshipyard"]) if env is not None else "?"
            lines.append(f"job-preferbuild {_JOB_ENV_RE.match(sel).group(1)} {attrs}")
        else:
            lines.append(f"other-op {op.tag} {sel}")


def summarise_mapdefaults(root, lines):
    for op in root:
        if not x4xml.is_element(op):
            continue
        sel = op.get("sel", "")
        m = re.search(r"dataset\[@macro='([^']+)'\]", sel)
        macro = m.group(1) if m else (op.find(".//dataset").get("macro") if op.find(".//dataset") is not None else sel)
        areas = sorted(
            f"{a.get('ref')}={a.get('amount')}" for a in op.iter("resourcearea")
        )
        if areas:
            lines.append(f"resources {macro}: " + ", ".join(areas))
        else:
            lines.append(f"other-op {op.tag} {sel}")


def summarise_clusters(root, lines):
    for op in root:
        if not x4xml.is_element(op):
            continue
        m = re.search(r"macro\[@name='([^']+)'\]", op.get("sel", ""))
        cluster = m.group(1) if m else op.get("sel", "")
        for conn in op.iter("connection"):
            region = conn.find(".//region")
            pos = conn.find(".//position")
            ref = region.get("ref") if region is not None else "?"
            at = f"({pos.get('x')},{pos.get('y')},{pos.get('z')})" if pos is not None else "?"
            lines.append(f"region {cluster}: {ref} @ {at}")


def summarise_plans(root, lines):
    for plan in root.findall("plan"):
        entries = len(plan.findall("entry"))
        lines.append(f"plan {plan.get('id')} entries={entries}")


def summarise_placedobjects(root, lines):
    for op in root:
        if not x4xml.is_element(op):
            continue
        for ship in op.iter("create_ship"):
            macro = (ship.get("macro") or "").removeprefix("macro.")
            sector = None
            for fs in op.iter("find_sector"):
                sector = (fs.get("macro") or "").removeprefix("macro.")
            pos = ship.find("position")
            at = f"({pos.get('x')},{pos.get('y')},{pos.get('z')})" if pos is not None else "?"
            loadout = ship.find("loadout")
            ref = f" loadout={loadout.get('ref')}" if loadout is not None else ""
            lines.append(f"ship {macro} @ {sector} {at}{ref}")


def summarise_loadouts(root, lines):
    for op in root:
        if not x4xml.is_element(op):
            continue
        for lo in op.iter("loadout"):
            macros = len(lo.find("macros")) if lo.find("macros") is not None else 0
            groups = len(lo.find("groups")) if lo.find("groups") is not None else 0
            lines.append(f"loadout {lo.get('id')} macro={lo.get('macro')} macros={macros} groups={groups}")


def summarise_regions(root, lines):
    for region in root.findall("region"):
        lines.append(f"region-def {region.get('name')}")


def generic(root, lines):
    for op in root:
        if x4xml.is_element(op):
            lines.append(f"op {op.tag} {op.get('sel', '')}")


SECTION_HANDLERS = [
    ("libraries/god.xml", summarise_god),
    ("libraries/jobs.xml", summarise_jobs),
    ("libraries/mapdefaults.xml", summarise_mapdefaults),
    ("clusters.xml", summarise_clusters),  # matched by suffix, covers DLC cluster files
    ("libraries/constructionplans.xml", summarise_plans),
    ("md/placedobjects.xml", summarise_placedobjects),
    ("libraries/loadouts.xml", summarise_loadouts),
    ("libraries/region_definitions.xml", summarise_regions),
]


def handler_for(relpath):
    for suffix, handler in SECTION_HANDLERS:
        if relpath.endswith(suffix):
            return handler
    return generic


def summarise(mod_dir):
    out = []
    for r, _, fs in os.walk(mod_dir):
        for f in sorted(fs):
            if not f.endswith(".xml") or f == "content.xml":
                continue
            relpath = os.path.relpath(os.path.join(r, f), mod_dir)
            lines = []
            handler_for(relpath)(x4xml.load(os.path.join(mod_dir, relpath)), lines)
            out.append((relpath, sorted(lines)))
    text = []
    for relpath, lines in sorted(out):
        text.append(f"== {relpath} ({len(lines)}) ==")
        text.extend(lines)
        text.append("")
    return "\n".join(text)


if __name__ == "__main__":
    mod_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(REPO, "mod", "after_the_fall")
    print(summarise(mod_dir))
