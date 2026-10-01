#!/usr/bin/env python3

import re
import sys
from pathlib import Path
from urllib.parse import unquote


ROOT = Path(__file__).resolve().parents[1]
EXCLUDED = (
    ".claude/",
    "artifacts/",
    "src/System.Web.ReferenceSource/",
    "src/System.Web.ApplicationServices.ReferenceSource/",
    "src/System.Web.Services.ReferenceSource/",
    "third_party/",
)
# Library content an application carries verbatim, and build output copied from it: their links
# point into the upstream repository layout and are not ours to repair.
VENDORED = frozenset({"bin", "obj", "packages", "Scripts"})
LINK = re.compile(r"(?<!!)\[[^\]]*\]\(([^)]+)\)")
HEADING = re.compile(r"^ {0,3}#{1,6}\s+(.+?)\s*#*\s*$", re.MULTILINE)
PROJECT_STATE = re.compile(r"^(?:Status|Priority):", re.MULTILINE)


def markdown_files():
    for path in ROOT.rglob("*.md"):
        relative = path.relative_to(ROOT)
        if relative.as_posix().startswith(EXCLUDED):
            continue
        if VENDORED.isdisjoint(relative.parts):
            yield path


def local_reference(raw):
    raw = raw.strip()
    if raw.startswith("<") and ">" in raw:
        raw = raw[1 : raw.index(">")]
    else:
        raw = raw.split(maxsplit=1)[0]
    if raw.startswith(("http://", "https://", "mailto:")):
        return None
    target, _, fragment = raw.partition("#")
    return unquote(target), unquote(fragment)


def anchors(text):
    result = set()
    counts = {}
    for match in HEADING.finditer(text):
        heading = re.sub(r"<[^>]+>", "", match.group(1))
        heading = re.sub(r"[`*_~]", "", heading).lower()
        slug = re.sub(r"[^\w\- ]", "", heading)
        slug = re.sub(r"\s+", "-", slug.strip())
        duplicate = counts.get(slug, 0)
        counts[slug] = duplicate + 1
        result.add(slug if duplicate == 0 else f"{slug}-{duplicate}")
    return result


def main():
    errors = []
    anchor_cache = {}
    allowed_state = {ROOT / "ROADMAP.md", ROOT / "docs/dev/backlog.md"}

    for path in markdown_files():
        text = path.read_text(encoding="utf-8-sig")
        if path not in allowed_state and PROJECT_STATE.search(text):
            errors.append(f"{path.relative_to(ROOT)}: project status belongs in roadmap/backlog")

        for match in LINK.finditer(text):
            reference = local_reference(match.group(1))
            if reference is None:
                continue
            target, fragment = reference
            resolved = (path.parent / target).resolve() if target else path.resolve()
            if not resolved.exists():
                line = text.count("\n", 0, match.start()) + 1
                errors.append(f"{path.relative_to(ROOT)}:{line}: missing {target}")
                continue
            if fragment and resolved.suffix.lower() == ".md":
                if resolved not in anchor_cache:
                    anchor_cache[resolved] = anchors(resolved.read_text(encoding="utf-8-sig"))
                if fragment not in anchor_cache[resolved]:
                    line = text.count("\n", 0, match.start()) + 1
                    errors.append(
                        f"{path.relative_to(ROOT)}:{line}: missing anchor #{fragment} in "
                        f"{resolved.relative_to(ROOT)}"
                    )

    backlog = (ROOT / "docs/dev/backlog.md").read_text(encoding="utf-8")
    for path in sorted((ROOT / "docs/dev/follow-ups").glob("*.md")):
        if path.name not in backlog:
            errors.append(f"docs/dev/backlog.md: missing follow-up {path.name}")
        text = path.read_text(encoding="utf-8-sig")
        if PROJECT_STATE.search(text):
            errors.append(f"{path.relative_to(ROOT)}: follow-up carries duplicate status")

    if errors:
        print("\n".join(errors))
        return 1

    print("Documentation checks passed")
    return 0


if __name__ == "__main__":
    sys.exit(main())
