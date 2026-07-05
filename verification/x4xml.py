"""Shared X4 XML machinery for the verification tools.

Three capabilities, used by the CLI tools in this folder:
  - resolve():   evaluate the selector dialect X4 diff files use (absolute paths,
                 leading/mid-path '//', [@attr='value'] predicates, /@attr targets)
  - apply_diff() / merge() / build_virtual_doc():
                 reproduce the game's file layering - base file, then each
                 extension's same-path file applied in load order (diff files
                 patch, plain files with a matching root merge their children)
  - canonical(): normalise a document for order-insensitive comparison, applying
                 the explicit "order does not matter here" rules below

Only the python stdlib is used (no lxml on this machine).
"""

import copy
import re
import xml.etree.ElementTree as ET

# Official DLC load order (release order). Extensions found on disk but not
# listed here are appended alphabetically - order between independent adds
# doesn't affect selector resolution anyway.
EXTENSION_ORDER = [
    "ego_dlc_split",
    "ego_dlc_terran",
    "ego_dlc_pirate",
    "ego_dlc_boron",
    "ego_dlc_timelines",
    "ego_dlc_mini_01",
    "ego_dlc_mini_02",
]

DIFF_OP_TAGS = {"add", "replace", "remove"}


def load(path):
    """Parse an XML file keeping comments (they are part of our output)."""
    parser = ET.XMLParser(target=ET.TreeBuilder(insert_comments=True))
    return ET.parse(path, parser=parser).getroot()


def is_element(node):
    """True for real elements; False for comment nodes (whose tag is a function)."""
    return isinstance(node.tag, str)


# ---------------------------------------------------------------------------
# Selector resolution
# ---------------------------------------------------------------------------

_ATTR_RE = re.compile(r"^(.*?)/@([\w.-]+)$")
_OR_RE = re.compile(r"\[([^\]]*? or [^\]]*?)\]")
_AND_RE = re.compile(r"\[([^\]]*? and [^\]]*?)\]")
_NOT_RE = re.compile(r"\[not\((\w+)\)\]$")  # trailing [not(child)] only (vanilla uses this)


def split_attr(sel):
    """Split a selector into (element_path, attribute_name_or_None)."""
    m = _ATTR_RE.match(sel)
    if m:
        return m.group(1), m.group(2)
    return sel, None


def _expand_predicates(path):
    """Rewrite predicates ElementTree can't parse into ones it can:
    [a or b]  -> one path per alternative (results are unioned)
    [a and b] -> stacked predicates [a][b]
    """
    m = _AND_RE.search(path)
    if m:
        stacked = "][".join(p.strip() for p in m.group(1).split(" and "))
        return _expand_predicates(path[: m.start()] + "[" + stacked + "]" + path[m.end():])
    m = _OR_RE.search(path)
    if not m:
        return [path]
    out = []
    for alt in m.group(1).split(" or "):
        out.extend(_expand_predicates(path[: m.start()] + "[" + alt.strip() + "]" + path[m.end():]))
    return out


def resolve(root, sel):
    """Resolve the element part of a selector against a document root element.

    Returns (matching_elements, attr_name_or_None). The document root itself is
    addressable ('/jobs' or '//jobs' match a <jobs> root), which ElementTree
    can't express directly - so we evaluate from a synthetic wrapper element.
    Raises ValueError on selector syntax the resolver doesn't support.
    """
    path, attr = split_attr(sel)
    wrapper = ET.Element("__doc__")
    wrapper.append(root)

    if path.startswith("//"):
        patterns = _expand_predicates(".//" + path[2:])
    elif path.startswith("/"):
        patterns = _expand_predicates("./" + path[1:])
    else:
        patterns = _expand_predicates("./" + path)  # relative: treated as root-relative

    matches, seen = [], set()
    try:
        for pattern in patterns:
            not_child = None
            m = _NOT_RE.search(pattern)
            if m:  # ElementTree has no not(): evaluate without it, then filter
                not_child = m.group(1)
                pattern = pattern[: m.start()] + pattern[m.end():]
            for node in wrapper.findall(pattern):
                if not_child is not None and node.find(not_child) is not None:
                    continue
                if id(node) not in seen:
                    seen.add(id(node))
                    matches.append(node)
    except SyntaxError as ex:
        raise ValueError(f"unsupported selector {sel!r}: {ex}") from ex
    finally:
        wrapper.remove(root)
    return matches, attr


def _parent_map(root):
    return {child: parent for parent in root.iter() for child in parent}


# ---------------------------------------------------------------------------
# Diff application and library merging (the game's file layering)
# ---------------------------------------------------------------------------


def apply_diff(base_root, diff_root, source, strict=True):
    """Apply an X4 <diff> document to base_root (mutating it).

    strict=True (our generated files): every op must resolve to exactly one
    node. strict=False (vanilla DLC layering): ops may match several nodes -
    vanilla diffs use [... or ...] selectors with multiple targets - and the
    op is applied to all of them; only zero matches is a problem.

    Returns a list of problem strings.
    """
    problems = []
    for op in diff_root:
        if not is_element(op):
            continue  # comments between ops
        if op.tag not in DIFF_OP_TAGS:
            problems.append(f"{source}: unknown diff operation <{op.tag}>")
            continue

        sel = op.get("sel")
        if not sel:
            problems.append(f"{source}: <{op.tag}> without sel attribute")
            continue

        try:
            nodes, attr = resolve(base_root, sel)
        except ValueError as ex:
            problems.append(f"{source}: {ex}")
            continue
        if (len(nodes) != 1) if strict else (len(nodes) == 0):
            problems.append(
                f"{source}: sel matches {len(nodes)} nodes"
                f" (need {'exactly 1' if strict else 'at least 1'}): {sel}"
            )
            continue

        pos = op.get("pos")
        if pos not in (None, "before", "after", "prepend"):
            problems.append(f"{source}: unknown pos={pos!r}: {sel}")
            continue

        for target in nodes:
            new_children = [copy.deepcopy(child) for child in op]

            if op.tag == "add":
                if attr is not None:
                    target.set(attr, (op.text or "").strip())
                elif pos is None:
                    for child in new_children:
                        target.append(child)
                elif pos == "prepend":
                    for i, child in enumerate(new_children):
                        target.insert(i, child)
                else:  # before / after: target is the sibling anchor
                    parent = _parent_map(base_root).get(target)
                    if parent is None:
                        problems.append(f"{source}: pos={pos} on the document root: {sel}")
                        continue
                    idx = list(parent).index(target) + (1 if pos == "after" else 0)
                    for i, child in enumerate(new_children):
                        parent.insert(idx + i, child)

            elif op.tag == "replace":
                if attr is not None:
                    if target.get(attr) is None:
                        problems.append(f"{source}: replacing attribute that does not exist: {sel}")
                    target.set(attr, (op.text or "").strip())
                else:
                    parent = _parent_map(base_root).get(target)
                    if parent is None:
                        problems.append(f"{source}: cannot replace the document root: {sel}")
                        continue
                    idx = list(parent).index(target)
                    parent.remove(target)
                    for i, child in enumerate(new_children):
                        parent.insert(idx + i, child)

            elif op.tag == "remove":
                if attr is not None:
                    if target.get(attr) is None:
                        problems.append(f"{source}: removing attribute that does not exist: {sel}")
                    target.attrib.pop(attr, None)
                else:
                    parent = _parent_map(base_root).get(target)
                    if parent is None:
                        problems.append(f"{source}: cannot remove the document root: {sel}")
                        continue
                    parent.remove(target)

    return problems


def merge(base_root, ext_root):
    """Root-merge a plain (non-diff) library file into the base document:
    the extension root's children are appended, as the game does for
    cumulative libraries (e.g. the split DLC's plain jobs.xml)."""
    for child in ext_root:
        base_root.append(copy.deepcopy(child))


def build_virtual_doc(vanilla_dir, relpath, extra_problems=None):
    """Build the game's merged document for a virtual file path: the vanilla
    file, then every extension's same-path file applied in load order.
    Works for core paths ('libraries/god.xml') and for extension-scoped paths
    ('extensions/ego_dlc_split/libraries/mapdefaults.xml', which later
    extensions could mirror-patch). Returns None if no vanilla file exists."""
    import os

    base_path = os.path.join(vanilla_dir, relpath)
    if not os.path.exists(base_path):
        return None
    doc = load(base_path)

    found = sorted(
        d for d in os.listdir(os.path.join(vanilla_dir, "extensions"))
        if os.path.isdir(os.path.join(vanilla_dir, "extensions", d))
    ) if os.path.isdir(os.path.join(vanilla_dir, "extensions")) else []
    order = [e for e in EXTENSION_ORDER if e in found] + [e for e in found if e not in EXTENSION_ORDER]

    for ext in order:
        ext_path = os.path.join(vanilla_dir, "extensions", ext, relpath)
        if not os.path.exists(ext_path):
            continue
        ext_root = load(ext_path)
        if ext_root.tag == "diff":
            problems = apply_diff(doc, ext_root, f"vanilla:{ext}/{relpath}", strict=False)
            if extra_problems is not None:
                extra_problems.extend(problems)
        elif ext_root.tag == doc.tag:
            merge(doc, ext_root)
        elif extra_problems is not None:
            extra_problems.append(
                f"vanilla:{ext}/{relpath}: root <{ext_root.tag}> neither diff nor <{doc.tag}>"
            )
    return doc


# ---------------------------------------------------------------------------
# Canonicalisation for order-insensitive comparison
# ---------------------------------------------------------------------------
#
# Every sort rule below is an explicit claim that child order in that container
# is presentation, not semantics. The default is NO sorting. Sorts are stable,
# so children the rule considers equal keep their original relative order.
#
# 'diff' rules apply when comparing generated diff files; 'world' adds rules
# for comparing patched game documents (where our additions live inside the
# vanilla containers). MD script <actions> are deliberately never sorted -
# action order is semantic in mission director scripts.

_GOD_ADD_SELS = {"//god/stations", "//god/products"}


def _norm_key(el):
    return repr(norm(el))


def _diff_op_key(el):
    if not is_element(el):
        return ("#comment", "", "", _norm_key(el))
    return (el.tag, el.get("sel", ""), el.get("pos", ""), _norm_key(el))


def _child_sort_rule(el, ruleset):
    """Return a sort key function for el's children, or None to keep order."""
    if not is_element(el):
        return None
    if el.tag == "diff":
        return _diff_op_key
    if el.tag == "add" and el.get("sel") in _GOD_ADD_SELS:
        return lambda c: (c.get("id", "") if is_element(c) else "", _norm_key(c))
    if el.tag == "resourceareas":
        return lambda c: (c.get("ref", "") if is_element(c) else "", c.get("amount", "") if is_element(c) else "")
    if el.tag == "plans":
        return lambda c: (c.get("id", "") if is_element(c) else "", _norm_key(c))
    if el.tag == "regions":
        return lambda c: (c.get("name", "") if is_element(c) else "", _norm_key(c))
    if ruleset == "world":
        by_attr = {
            "stations": "id",
            "products": "id",
            "jobs": "id",
            "defaults": "macro",
            "loadouts": "id",
            "connections": "name",
        }
        attr = by_attr.get(el.tag)
        if attr is not None:
            return lambda c: (c.get(attr, "") if is_element(c) else "", _norm_key(c))
    return None


def norm(el):
    """Normalised comparable form of an element: whitespace-only text ignored,
    attribute order irrelevant (dict), children in document order."""
    if not is_element(el):
        return ("#comment", (el.text or "").strip())
    return (
        el.tag,
        dict(el.attrib),
        (el.text or "").strip(),
        tuple((norm(c), (c.tail or "").strip()) for c in el),
    )


def canonical(el, ruleset="diff"):
    """Like norm(), but with the sort rules applied - the basis of
    order-insensitive ('canonically equal') comparison."""
    if not is_element(el):
        return ("#comment", (el.text or "").strip())
    children = list(el)
    key = _child_sort_rule(el, ruleset)
    if key is not None:
        children = sorted(children, key=key)  # sorted() is stable
    return (
        el.tag,
        dict(el.attrib),
        (el.text or "").strip(),
        tuple((canonical(c, ruleset), (c.tail or "").strip()) for c in children),
    )


def first_difference(a, b, path="/"):
    """Human-readable location of the first difference between two norm/canonical
    structures, or None if they are equal."""
    if a == b:
        return None
    if a[0] != b[0]:
        return f"{path}: element <{a[0]}> vs <{b[0]}>"
    tag = a[0]
    if tag == "#comment":
        return f"{path}: comment text differs"
    if a[1] != b[1]:
        keys = {k for k in set(a[1]) | set(b[1]) if a[1].get(k) != b[1].get(k)}
        details = ", ".join(f"@{k}: {a[1].get(k)!r} vs {b[1].get(k)!r}" for k in sorted(keys))
        return f"{path}<{tag}>: {details}"
    if a[2] != b[2]:
        return f"{path}<{tag}>: text {a[2]!r} vs {b[2]!r}"
    ac, bc = a[3], b[3]
    if len(ac) != len(bc):
        return f"{path}<{tag}>: {len(ac)} children vs {len(bc)}"
    for i, ((ca, ta), (cb, tb)) in enumerate(zip(ac, bc)):
        if ca != cb:
            label = ca[0] if ca[0] == cb[0] else f"{ca[0]}|{cb[0]}"
            ident = ""
            if ca[0] != "#comment" and isinstance(ca[1], dict):
                ident = ca[1].get("id") or ca[1].get("name") or ca[1].get("sel") or ""
                ident = f"[{ident}]" if ident else f"[#{i}]"
            return first_difference(ca, cb, f"{path}{tag}/{label}{ident}/")
        if ta != tb:
            return f"{path}{tag}: trailing text of child #{i} differs"
    return f"{path}<{tag}>: differs (unlocated)"
