#!/usr/bin/env python3
"""Compare two mod output trees with tiered strictness.

Per file the result is one of:
  byte       - byte-identical
  canonical  - same content after the explicit order/whitespace normalisation
               rules in x4xml.py (a pure reordering/reformatting change)
  DIFFERS    - semantically different (first difference is reported)

Usage: compare_canonical.py BASELINE_DIR NEW_DIR [--require byte|canonical]
                            [--rules diff|world] [--quiet]

Exit code 0 when every file meets the required tier (default: canonical).
Non-XML files must always be byte-identical.

Self-test (proves the comparator accepts pure reorders and rejects value
changes): compare_canonical.py --selftest MOD_OUTPUT_DIR
"""

import filecmp
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import x4xml

SKIP = {".DS_Store"}


def listing(root):
    return {
        os.path.relpath(os.path.join(r, f), root)
        for r, _, fs in os.walk(root)
        for f in fs
        if f not in SKIP
    }


def compare_dirs(base, new, require="canonical", rules="diff", quiet=False):
    tiers = {"byte": 0, "canonical": 1}
    required = tiers[require]
    failures = []

    def report(tier, rel, detail=""):
        if not quiet or tier == "DIFFERS":
            print(f"{tier:<10} {rel}{'  ' + detail if detail else ''}")

    bfiles, nfiles = listing(base), listing(new)
    for missing in sorted(bfiles - nfiles):
        failures.append(f"missing in new: {missing}")
    for extra in sorted(nfiles - bfiles):
        failures.append(f"extra in new: {extra}")

    for rel in sorted(bfiles & nfiles):
        p1, p2 = os.path.join(base, rel), os.path.join(new, rel)
        if filecmp.cmp(p1, p2, shallow=False):
            report("byte", rel)
            continue
        if not rel.endswith(".xml"):
            failures.append(f"{rel}: non-XML file differs")
            report("DIFFERS", rel, "(non-XML)")
            continue
        try:
            a = x4xml.canonical(x4xml.load(p1), rules)
            b = x4xml.canonical(x4xml.load(p2), rules)
        except Exception as ex:  # noqa: BLE001 - parse errors are findings
            failures.append(f"{rel}: parse error: {ex}")
            report("DIFFERS", rel, f"(parse error: {ex})")
            continue
        if a == b:
            report("canonical", rel)
            if required < tiers["canonical"]:
                failures.append(f"{rel}: not byte-identical (canonical only)")
        else:
            where = x4xml.first_difference(a, b) or "?"
            failures.append(f"{rel}: {where}")
            report("DIFFERS", rel, where)

    return failures


def selftest(mod_dir):
    """Shuffle containers the rules declare order-free (expect: canonical),
    then mutate one attribute value (expect: DIFFERS)."""
    import random
    import shutil
    import tempfile
    import xml.etree.ElementTree as ET

    god_rel = "libraries/god.xml"
    src = os.path.join(mod_dir, god_rel)
    rng = random.Random(42)

    with tempfile.TemporaryDirectory() as tmp:
        base = os.path.join(tmp, "base")
        shutil.copytree(mod_dir, base)

        # 1. reorder: shuffle diff-root ops and station adds
        shuffled = os.path.join(tmp, "shuffled")
        shutil.copytree(mod_dir, shuffled)
        root = x4xml.load(src)
        ops = list(root)
        rng.shuffle(ops)
        for op in list(root):
            root.remove(op)
        for op in ops:
            root.append(op)
        for add in root.iter("add"):
            kids = list(add)
            rng.shuffle(kids)
            for k in list(add):
                add.remove(k)
            for k in kids:
                add.append(k)
        ET.ElementTree(root).write(os.path.join(shuffled, god_rel), encoding="utf-8")
        failures = compare_dirs(base, shuffled, require="canonical", quiet=True)
        if failures:
            print("SELFTEST FAILED: pure reorder was not accepted as canonical:")
            for f in failures:
                print("  " + f)
            return 1
        print("selftest 1 ok: shuffled god.xml ops/stations judged canonically equal")

        # 2. mutate: change one quota value
        mutated = os.path.join(tmp, "mutated")
        shutil.copytree(mod_dir, mutated)
        root = x4xml.load(src)
        quota = next(el for el in root.iter("quota") if el.get("galaxy"))
        quota.set("galaxy", str(int(quota.get("galaxy")) + 1))
        ET.ElementTree(root).write(os.path.join(mutated, god_rel), encoding="utf-8")
        failures = compare_dirs(base, mutated, require="canonical", quiet=True)
        if not failures:
            print("SELFTEST FAILED: a mutated quota value was NOT detected")
            return 1
        print(f"selftest 2 ok: mutated quota detected ({failures[0]})")
    return 0


def main(argv):
    if "--selftest" in argv:
        argv.remove("--selftest")
        return selftest(argv[0])

    require, rules, quiet = "canonical", "diff", False
    args = []
    i = 0
    while i < len(argv):
        if argv[i] == "--require":
            require = argv[i + 1]
            i += 2
        elif argv[i] == "--rules":
            rules = argv[i + 1]
            i += 2
        elif argv[i] == "--quiet":
            quiet = True
            i += 1
        else:
            args.append(argv[i])
            i += 1
    base, new = args

    failures = compare_dirs(base, new, require, rules, quiet)
    if failures:
        print(f"\nFAILED ({len(failures)} problem(s) at tier '{require}'):")
        for f in failures:
            print("  " + f)
        return 1
    print(f"\nOK: all files meet tier '{require}'")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
