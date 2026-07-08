#!/usr/bin/env python3
"""Apply the generated mod to the vanilla game files, the way the game would.

For every XML file in the mod output:
  - build the virtual base document (vanilla file + each DLC's same-path file
    applied in load order), then
  - diff files: apply every operation, requiring each selector to resolve to
    exactly one node (a selector that matches nothing is the classic silently
    broken mod);
  - plain library files: root-merge, checking for id collisions with vanilla.

Post-patch sanity checks: unique station/product/job/plan/region identifiers
(only duplicates INTRODUCED by the mod fail - the merged vanilla data already
contains duplicate demo_/timelines datasets).

Schema validation (xmllint): patched MD scripts against the game's md.xsd.
(diff.xsd itself does not compile under libxml2 - an entity-reference loop -
so diff files get a structural check instead: root <diff>, only
add/replace/remove ops, sel present, pos valid. apply_diff enforces this.)

Problems in the VANILLA layering (unsupported vanilla selector syntax, vanilla
ops that match nothing) are reported as warnings, not failures - they reflect
resolver limits or vanilla quirks, not our mod.

Usage: simulate.py [--mod MOD_DIR] [--vanilla VANILLA_DIR]
                   [--dump DIR]   write the patched world documents to DIR
                   [--quiet]      only report problems
Exit 0 when no problems were found.
"""

import os
import subprocess
import sys
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import x4xml

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SKIP_FILES = {"content.xml"}  # mod metadata, not game content

# Identifier uniqueness rules for patched documents: root tag -> (path, attr)
UNIQUE_IDS = {
    "god": [("./stations/station", "id"), ("./products/product", "id")],
    "jobs": [("./job", "id")],
    "plans": [("./plan", "id")],
    "regions": [("./region", "name")],
    "defaults": [("./dataset", "macro")],
    "loadouts": [("./loadout", "id")],
}


def mod_xml_files(mod_dir):
    for r, _, fs in os.walk(mod_dir):
        for f in sorted(fs):
            if f.endswith(".xml") and f not in SKIP_FILES:
                yield os.path.relpath(os.path.join(r, f), mod_dir)


def collect_duplicates(doc):
    """The set of duplicated identifiers in a document, per the UNIQUE_IDS rules."""
    if doc is None:
        return set()
    dups = set()
    for path, attr in UNIQUE_IDS.get(doc.tag, []):
        seen = set()
        for el in doc.findall(path):
            key = el.get(attr)
            if key is None:
                continue
            if key in seen:
                dups.add((path.split("/")[-1], attr, key))
            seen.add(key)
    return dups


def xmllint(schema, xml_path):
    """Validate a file with xmllint. Returns None on success, message on failure."""
    result = subprocess.run(
        ["xmllint", "--noout", "--schema", schema, xml_path],
        capture_output=True, text=True,
    )
    if result.returncode == 0:
        return None
    tail = (result.stderr or result.stdout).strip().splitlines()
    return "; ".join(tail[-3:])


def simulate(mod_dir, vanilla_dir, dump_dir=None, quiet=False):
    problems = []
    warnings = []
    op_total = 0
    md_xsd = os.path.join(vanilla_dir, "libraries", "md.xsd")

    # md.xsd validation is advisory unless vanilla itself validates cleanly.
    md_schema_trustworthy = os.path.exists(md_xsd) and xmllint(
        md_xsd, os.path.join(vanilla_dir, "md", "placedobjects.xml")
    ) is None

    for relpath in mod_xml_files(mod_dir):
        mod_path = os.path.join(mod_dir, relpath)
        mod_root = x4xml.load(mod_path)
        doc = x4xml.build_virtual_doc(vanilla_dir, relpath, warnings)
        pre_duplicates = collect_duplicates(doc)

        if mod_root.tag == "diff":
            if doc is None:
                problems.append(f"{relpath}: diff file but no vanilla file exists at this path")
                continue
            ops = sum(1 for op in mod_root if x4xml.is_element(op))
            op_total += ops
            file_problems = x4xml.apply_diff(doc, mod_root, relpath)
            problems += file_problems
            if not quiet:
                status = "OK" if not file_problems else f"{len(file_problems)} PROBLEM(S)"
                print(f"  {relpath}: {ops} ops -> {status}")
        else:
            if doc is None:
                doc = mod_root  # a brand new file the mod introduces
                if not quiet:
                    print(f"  {relpath}: new <{mod_root.tag}> file (no vanilla counterpart)")
            elif doc.tag != mod_root.tag:
                problems.append(f"{relpath}: root <{mod_root.tag}> does not match vanilla <{doc.tag}>")
                continue
            else:
                x4xml.merge(doc, mod_root)
                if not quiet:
                    kids = sum(1 for c in mod_root if x4xml.is_element(c))
                    print(f"  {relpath}: merged {kids} <{mod_root.tag}> children into vanilla")

        for tag, attr, key in sorted(collect_duplicates(doc) - pre_duplicates):
            problems.append(f"{relpath}: mod introduces duplicate {tag} {attr}={key!r}")

        if doc.tag == "mdscript" and md_schema_trustworthy and dump_dir is None:
            # validate the patched MD script (needs a temp file for xmllint)
            import tempfile

            with tempfile.NamedTemporaryFile(suffix=".xml", delete=False) as tmp:
                ET.ElementTree(doc).write(tmp.name, encoding="utf-8")
                err = xmllint(md_xsd, tmp.name)
                os.unlink(tmp.name)
            if err:
                problems.append(f"{relpath}: patched MD script fails md.xsd: {err}")

        if dump_dir is not None:
            out = os.path.join(dump_dir, relpath)
            os.makedirs(os.path.dirname(out), exist_ok=True)
            ET.ElementTree(doc).write(out, encoding="utf-8")

    return problems, warnings, op_total


def main(argv):
    mod_dir = os.path.join(REPO, "mod", "after_the_fall")
    vanilla_dir = os.path.join(REPO, "X4_unpacked_data", "9.0")
    dump_dir = None
    quiet = False
    i = 0
    while i < len(argv):
        if argv[i] == "--mod":
            mod_dir = argv[i + 1]
            i += 2
        elif argv[i] == "--vanilla":
            vanilla_dir = argv[i + 1]
            i += 2
        elif argv[i] == "--dump":
            dump_dir = argv[i + 1]
            i += 2
        elif argv[i] == "--quiet":
            quiet = True
            i += 1
        else:
            print(f"unknown arg {argv[i]}")
            return 2
        continue

    if not quiet:
        print(f"simulating {mod_dir} against {vanilla_dir}")

    # The baseline is stock game + official DLC only. A foreign extension in the
    # unpack (a subscribed workshop mod, or our own mod copied back in by a full
    # re-extract) is ignored, not layered in - warn so it's never a silent trap.
    _official, foreign = x4xml.classify_extensions(vanilla_dir)
    if foreign:
        print(f"NOTE: ignoring {len(foreign)} non-vanilla extension(s) in the unpack, "
              f"not part of the baseline: {', '.join(foreign)}")

    problems, warnings, ops = simulate(mod_dir, vanilla_dir, dump_dir, quiet)
    if warnings and not quiet:
        print(f"\n{len(warnings)} vanilla-layering warning(s) (not failures):")
        for w in warnings:
            print("  " + w)
    if problems:
        print(f"\nFAILED: {len(problems)} problem(s) across {ops} diff ops:")
        for p in problems:
            print("  " + p)
        return 1
    print(f"\nOK: all {ops} mod diff ops resolved and applied cleanly; no new duplicate ids; schemas pass")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
