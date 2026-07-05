# Verification harness

Layered correctness checks for the mod generator, so changes can be verified at
the *right* strictness instead of always requiring byte-identical output.

```
verification/verify.sh                 # build, run, verify (canonical tier by default)
verification/verify.sh --byte          # strictest: for pure code motion
verification/verify.sh --world         # loosest diff-vs-baseline: compares patched game docs
verification/verify.sh --baseline v1.2 # compare against any git ref (default HEAD)
verification/verify.sh --update-golden # accept a deliberate balance change into the golden summary
```

## The tiers (choose per change)

| Tier | Passes when | Use for |
|---|---|---|
| `--byte` | output bytes unchanged | pure code motion, renames |
| `--canonical` (default) | same content after explicit order/whitespace rules | reordering refactors, template restructuring |
| `--world` | patched game documents canonically equal | representation changes (e.g. one `replace` becomes `remove`+`add`) |

The baseline is **generated from a git ref** (default `HEAD`) in a temporary
worktree and cached in `verification/cache/<sha>/` — it can never go stale.
Typical flow: make changes, run `verify.sh`, commit when green.

## Baseline-free layers (always run unless `--skip-sim`)

- **Patch simulation** ([simulate.py](simulate.py)): applies every generated file to the
  vanilla 9.0 data the way the game does (vanilla file + each DLC's same-path
  file in load order, diffs patch / plain library files root-merge), requiring
  every selector to resolve to exactly one node. This catches the classic
  silently-broken mod. Post-patch it checks id uniqueness (stations, products,
  jobs, plans, regions, datasets, loadouts).
- **Schema validation**: every diff file against the game's `diff.xsd`;
  patched MD scripts against `md.xsd` (advisory if vanilla itself fails it).
- **World summary** ([world_summary.py](world_summary.py)): a sorted, human-readable digest of
  the mod's decisions (stations added/removed/moved, quotas, resources, ships)
  diffed against the committed [world_summary.golden](world_summary.golden). A deliberate balance
  change shows up as a small readable diff — review it, then `--update-golden`.

## Canonicalisation rules (the "order doesn't matter here" claims)

Declared in [x4xml.py](x4xml.py) `_child_sort_rule`; the default is **no sorting**.
Currently sorted: diff-root operations (keyed by tag+sel+content), station and
product adds in god.xml (by id), `resourceareas` (by ref), `plans` (by id),
`regions` (by name). MD `<actions>` are deliberately never sorted — action
order is semantic in mission director scripts, so a reorder of the abandoned-ship
adds passes at canonical tier (same-sel ops sort by content) but shows up at
world tier. If a new output container's order is presentation-only, add a rule
there; each rule is a reviewable claim.

`compare_canonical.py --selftest mod/after_the_fall` proves the comparator
accepts a pure reorder and rejects a value mutation.

## F# logic tests

`dotnet test` in `X4MLParser.Tests/` covers the pure logic layer (job quota
scaling rules, station decisions, bastion counts) with no XML and no baseline.
