#!/usr/bin/env python3

import re
import sys
from pathlib import Path
from urllib.parse import unquote


ROOT = Path(__file__).resolve().parents[1]
EXCLUDED = (
    "src/System.Web.ReferenceSource/",
    "src/System.Web.ApplicationServices.ReferenceSource/",
    "src/System.Web.Services.ReferenceSource/",
    "third_party/",
)
LINK = re.compile(r"(?<!!)\[[^\]]*\]\(([^)]+)\)")
PROJECT_STATE = re.compile(r"^(?:Status|Priority):", re.MULTILINE)


def markdown_files():
    for path in ROOT.rglob("*.md"):
        relative = path.relative_to(ROOT).as_posix()
        if not relative.startswith(EXCLUDED):
            yield path


def local_target(raw):
    raw = raw.strip()
    if raw.startswith("<") and ">" in raw:
        raw = raw[1 : raw.index(">")]
    else:
        raw = raw.split(maxsplit=1)[0]
    if raw.startswith(("#", "http://", "https://", "mailto:")):
        return None
    return unquote(raw.split("#", 1)[0])


def main():
    errors = []
    allowed_state = {ROOT / "ROADMAP.md", ROOT / "docs/backlog.md"}

    for path in markdown_files():
        text = path.read_text(encoding="utf-8-sig")
        if path not in allowed_state and PROJECT_STATE.search(text):
            errors.append(f"{path.relative_to(ROOT)}: project status belongs in roadmap/backlog")

        for match in LINK.finditer(text):
            target = local_target(match.group(1))
            if not target:
                continue
            resolved = (path.parent / target).resolve()
            if not resolved.exists():
                line = text.count("\n", 0, match.start()) + 1
                errors.append(f"{path.relative_to(ROOT)}:{line}: missing {target}")

    backlog = (ROOT / "docs/backlog.md").read_text(encoding="utf-8")
    for path in sorted((ROOT / "docs/follow-ups").glob("*.md")):
        if path.name not in backlog:
            errors.append(f"docs/backlog.md: missing follow-up {path.name}")
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
