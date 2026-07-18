#!/usr/bin/env python3
"""Extract and aggregate the first-pass System.Web build diagnostics."""

from __future__ import annotations

import argparse
import csv
import json
import re
from collections import Counter
from pathlib import Path


DIAGNOSTIC = re.compile(
    r"^\s*(?P<file>.+?)\((?P<line>\d+),(?P<column>\d+)\): "
    r"(?P<severity>error|warning) (?P<code>[A-Z]+\d+): "
    r"(?P<message>.*?)(?: \[(?P<project>.+)\])?$"
)

CASCADE_CODES = {"CS0115", "CS0118", "CS0182", "CS0538", "CS0616", "CS0641", "CS1503"}


def classify_error(code: str, message: str) -> str:
    if code in CASCADE_CODES:
        return "secondary/cascading compiler diagnostics"

    generation_markers = (
        "'SR'", "'AssemblyRef'", "'ModName'", "'CacheUsage'", "'CacheExpires'",
        "'UsageEntryRef'", "'ExpiresEntryRef'", "'RegularExpressions'",
        "'Resources' does not exist in the namespace 'System.ComponentModel.DataAnnotations'",
        "'Tools' does not exist in the namespace 'System.Resources'",
    )
    if any(marker in message for marker in generation_markers):
        return "source, preprocessing, and resource generation"

    configuration_markers = (
        "System.Configuration", "ConfigurationCollection", "ConfigurationValidator",
        "TimeSpanValidatorAttribute", "ConfigurationElementCollectionType",
        "IInternalConfigRecord", "'ConfigurationPropertyOptions'",
        "'Configuration' does not exist in the namespace 'System.Net'",
    )
    if any(marker in message for marker in configuration_markers):
        return "configuration dependencies"

    codedom_markers = (
        "System.CodeDom", "Microsoft.Build", "BuildErrorEventArgs", "BuildWarningEventArgs",
        "BuildMessageEventArgs", "CustomBuildEventArgs", "IBuildEngine",
        "TargetDotNetFrameworkVersion", "'Build' does not exist in the namespace 'Microsoft'",
    )
    if any(marker in message for marker in codedom_markers):
        return "CodeDOM and runtime compilation"

    isolation_markers = (
        "System.Security.Permissions", "PermissionSet", "AspNetHostingPermission",
        "HostProtection", "Unrestricted", "FileIOPermissionAccess",
        "ReflectionPermissionFlag", "System.Runtime.Remoting", "IObjectHandle",
        "AppDomainManager", "IFormatter", "BinaryFormatter",
    )
    if any(marker in message for marker in isolation_markers):
        return "AppDomain, remoting, CAS, and serialization"

    windows_markers = (
        "DirectoryServices", "DirectoryEntry", "DirectoryAttribute", "SearchResult",
        "LdapConnection", "AuthenticationTypes", "AuthType", "EnterpriseServices",
        "TransactionOption", "UITypeEditor", "ToolboxBitmap", "System.Drawing",
        "System.Data.Design", "System.Net.Configuration", "IApplicationIdentifier",
        "ITypeLibExporterNotifySink", "ExporterEventKind",
        "'Design' does not exist in the namespace 'System.Web.UI'",
        "'Design' does not exist in the namespace 'System.Data'",
    )
    if any(marker in message for marker in windows_markers):
        return "IIS, Windows, native, and design-time dependencies"

    package_markers = (
        "System.Data.SqlClient", "System.Runtime.Caching", "MemoryCache",
        "IMemoryCacheManager", "IFileChangeNotificationSystem", "OnChangedCallback",
        "Membership", "RoleProvider", "System.Web.Services", "DataProtector",
        "'Services' does not exist in the namespace 'System.Web'",
        "'Caching' does not exist in the namespace 'System.Runtime'",
        "ValidatePasswordEventArgs",
    )
    if any(marker in message for marker in package_markers):
        return "missing assemblies or packages"

    removed_markers = (
        "ICustomLoaderHelperFunctions", "System.Web.UI.Design",
        "BitmapSuffixInSatelliteAssemblyAttribute", "IMembershipAdapter",
    )
    if any(marker in message for marker in removed_markers):
        return "removed or inaccessible BCL/framework APIs"

    return "other source or unresolved dependency"


def subsystem(relative_file: str) -> str:
    prefix = "src/System.Web.ReferenceSource/"
    path = relative_file.removeprefix(prefix)
    first = path.split("/", 1)[0]
    return "root" if first.endswith(".cs") else first


def write_tsv(path: Path, header: list[str], rows: list[tuple[object, ...]]) -> None:
    with path.open("w", encoding="utf-8", newline="") as output:
        writer = csv.writer(output, delimiter="\t", lineterminator="\n")
        writer.writerow(header)
        writer.writerows(rows)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("log", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--root", type=Path, required=True)
    args = parser.parse_args()

    root = args.root.resolve()
    diagnostics: list[dict[str, object]] = []
    for raw_line in args.log.read_text(encoding="utf-8", errors="replace").splitlines():
        match = DIAGNOSTIC.match(raw_line)
        if not match:
            continue
        item = match.groupdict()
        file_path = Path(item["file"])
        if not file_path.is_absolute():
            continue
        try:
            item["file"] = file_path.resolve().relative_to(root).as_posix()
        except ValueError:
            item["file"] = file_path.as_posix()
        item["line"] = int(item["line"])
        item["column"] = int(item["column"])
        item["subsystem"] = subsystem(str(item["file"]))
        item["category"] = (
            classify_error(str(item["code"]), str(item["message"]))
            if item["severity"] == "error"
            else "warning"
        )
        diagnostics.append(item)

    args.output.mkdir(parents=True, exist_ok=True)

    diagnostics.sort(key=lambda item: (
        str(item["severity"]), str(item["file"]), int(item["line"]),
        int(item["column"]), str(item["code"]), str(item["message"]),
    ))
    write_tsv(
        args.output / "diagnostics.tsv",
        ["severity", "code", "file", "line", "column", "subsystem", "category", "message"],
        [tuple(item[key] for key in (
            "severity", "code", "file", "line", "column", "subsystem", "category", "message"
        )) for item in diagnostics],
    )

    message_counts = Counter(
        (str(item["severity"]), str(item["code"]), str(item["message"]))
        for item in diagnostics
    )
    write_tsv(
        args.output / "message-groups.tsv",
        ["count", "severity", "code", "message"],
        [(count, severity, code, message) for (severity, code, message), count in sorted(
            message_counts.items(), key=lambda pair: (-pair[1], pair[0])
        )],
    )

    error_categories = Counter(
        str(item["category"]) for item in diagnostics if item["severity"] == "error"
    )
    write_tsv(
        args.output / "error-categories.tsv",
        ["count", "category"],
        [(count, category) for category, count in sorted(
            error_categories.items(), key=lambda pair: (-pair[1], pair[0])
        )],
    )

    subsystem_counts = Counter(
        (str(item["severity"]), str(item["subsystem"])) for item in diagnostics
    )
    write_tsv(
        args.output / "subsystems.tsv",
        ["count", "severity", "subsystem"],
        [(count, severity, name) for (severity, name), count in sorted(
            subsystem_counts.items(), key=lambda pair: (-pair[1], pair[0])
        )],
    )

    forwarded = Counter()
    for item in diagnostics:
        if item["severity"] != "error":
            continue
        match = re.search(r"forwarded to assembly '([^,']+)", str(item["message"]))
        if match:
            forwarded[match.group(1)] += 1
    write_tsv(
        args.output / "forwarded-assemblies.tsv",
        ["count", "assembly"],
        [(count, assembly) for assembly, count in sorted(
            forwarded.items(), key=lambda pair: (-pair[1], pair[0])
        )],
    )

    severity_counts = Counter(str(item["severity"]) for item in diagnostics)
    code_counts = Counter(
        (str(item["severity"]), str(item["code"])) for item in diagnostics
    )
    affected_files = {
        severity: len({str(item["file"]) for item in diagnostics if item["severity"] == severity})
        for severity in ("error", "warning")
    }
    summary = {
        "sourceLog": args.log.as_posix(),
        "diagnosticCount": len(diagnostics),
        "severityCounts": dict(sorted(severity_counts.items())),
        "affectedFileCounts": affected_files,
        "uniqueMessageGroups": len(message_counts),
        "codeCounts": {
            severity: {
                code: count for (item_severity, code), count in sorted(code_counts.items())
                if item_severity == severity
            }
            for severity in ("error", "warning")
        },
        "errorCategoryCounts": dict(sorted(error_categories.items())),
        "forwardedAssemblyCounts": dict(sorted(forwarded.items())),
    }
    (args.output / "summary.json").write_text(
        json.dumps(summary, indent=2, sort_keys=True) + "\n", encoding="utf-8"
    )


if __name__ == "__main__":
    main()
