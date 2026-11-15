#!/usr/bin/env python3
"""Turn JUnit + Cobertura results from every CI platform into one Markdown PR comment.

Expects --results to contain one folder per platform (test-results-<os>/), each holding the
*.junit.xml and coverage.cobertura*.xml files written by `dotnet test`.
"""
from __future__ import annotations

import argparse
import glob
import os
import xml.etree.ElementTree as ET
from collections import defaultdict

# file -> (covered lines, coverable lines)
Coverage = dict[str, tuple[int, int]]
# platform -> (total, failed, skipped)
TestSummary = dict[str, tuple[int, int, int]]
# (platform, test name, first line of the failure message)
Failure = tuple[str, str, str]

WORST_FILES_SHOWN = 5
FAILURES_SHOWN = 25


def repo_relative(path: str, top_dirs: set[str]) -> str | None:
    """Map an absolute (Windows or POSIX) source path from any runner to a repo-relative path, or None if it isn't ours."""
    parts = path.replace("\\", "/").split("/")
    for i, part in enumerate(parts):
        if part in top_dirs:
            return "/".join(parts[i:])
    return None


def read_coverage(files: list[str], top_dirs: set[str]) -> Coverage:
    """Union line hits across all reports: a line counts as covered if any platform/test project executed it."""
    lines: defaultdict[str, dict[int, bool]] = defaultdict(dict)  # file -> {line number: covered}
    for report in files:
        root = ET.parse(report).getroot()
        sources = [s.text or "" for s in root.iter("source")] or [""]
        for cls in root.iter("class"):
            filename = cls.get("filename", "")
            rel = None
            for source in sources:
                rel = repo_relative(os.path.join(source, filename), top_dirs)
                if rel:
                    break
            if not rel:
                continue
            for line in cls.iter("line"):
                number = int(line.get("number", "0"))
                hit = int(line.get("hits", "0")) > 0
                lines[rel][number] = lines[rel].get(number, False) or hit
    return {f: (sum(v.values()), len(v)) for f, v in lines.items()}


def read_tests(results_dir: str) -> tuple[TestSummary, list[Failure]]:
    """Per-platform pass/fail/skip counts and the list of failing tests."""
    summary: TestSummary = {}
    failures: list[Failure] = []
    for platform_dir in sorted(glob.glob(os.path.join(results_dir, "test-results-*"))):
        platform = os.path.basename(platform_dir).removeprefix("test-results-")
        total = failed = skipped = 0
        for report in glob.glob(os.path.join(platform_dir, "**", "*.junit.xml"), recursive=True):
            for case in ET.parse(report).getroot().iter("testcase"):
                total += 1
                failure = case.find("failure")
                if failure is None:
                    failure = case.find("error")
                if failure is not None:
                    failed += 1
                    message = (failure.get("message") or failure.text or "").strip().splitlines()
                    failures.append((platform, case.get("name", "?"), message[0][:200] if message else ""))
                elif case.find("skipped") is not None:
                    skipped += 1
        summary[platform] = (total, failed, skipped)
    return summary, failures


def pct(hit: int, total: int) -> float:
    return 100.0 * hit / total if total else 100.0


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--results", required=True)
    parser.add_argument("--changed", required=True, help="File listing paths changed by the PR, one per line")
    parser.add_argument("--repo-root", default=".")
    parser.add_argument("--output", required=True)
    args = parser.parse_args()

    top_dirs = {d for d in os.listdir(args.repo_root) if os.path.isdir(os.path.join(args.repo_root, d)) and not d.startswith(".")}
    coverage = read_coverage(glob.glob(os.path.join(args.results, "**", "coverage*.cobertura*.xml"), recursive=True), top_dirs)
    summary, failures = read_tests(args.results)
    with open(args.changed, encoding="utf-8") as f:
        changed = [line.strip() for line in f if line.strip().endswith(".cs")]

    out = ["## Test report", ""]
    if not summary:
        out.append(":warning: No test results were produced; check the workflow logs.")
    else:
        out += ["| Platform | Passed | Failed | Skipped |", "|---|---|---|---|"]
        for platform, (total, failed, skipped) in summary.items():
            icon = ":x:" if failed else ":white_check_mark:"
            out.append(f"| {icon} {platform} | {total - failed - skipped} | {failed} | {skipped} |")

    if failures:
        out += ["", f"### Failing tests ({len(failures)})", ""]
        for platform, name, message in failures[:FAILURES_SHOWN]:
            out.append(f"- **{platform}** `{name}`" + (f": {message}" if message else ""))
        if len(failures) > FAILURES_SHOWN:
            out.append(f"- ...and {len(failures) - FAILURES_SHOWN} more (see the workflow logs)")

    if coverage:
        hit = sum(h for h, _ in coverage.values())
        total = sum(t for _, t in coverage.values())
        out += ["", f"### Line coverage: {pct(hit, total):.1f}% ({hit}/{total} lines)", ""]
        per_project: defaultdict[str, list[int]] = defaultdict(lambda: [0, 0])
        for path, (h, t) in coverage.items():
            per_project[path.split("/")[0]][0] += h
            per_project[path.split("/")[0]][1] += t
        out += ["| Project | Line coverage |", "|---|---|"]
        out += [f"| {project} | {pct(h, t):.1f}% |" for project, (h, t) in sorted(per_project.items())]

    touched = sorted(((pct(*coverage[f]), f, *coverage[f]) for f in changed if f in coverage))
    # Files in a project with no coverage at all (as opposed to files with no executable lines, e.g. AssemblyInfo.cs)
    covered_projects = {path.split("/")[0] for path in coverage}
    untracked = [f for f in changed if coverage and f.split("/")[0] not in covered_projects and os.path.exists(os.path.join(args.repo_root, f)) and ".Tests/" not in f]
    if touched or untracked:
        out += ["", "### Least-covered files touched by this PR", "", "| File | Line coverage | Lines |", "|---|---|---|"]
        for percent, path, h, t in touched[:WORST_FILES_SHOWN]:
            out.append(f"| `{path}` | {percent:.1f}% | {h}/{t} |")
        for path in untracked[:WORST_FILES_SHOWN]:
            out.append(f"| `{path}` | no coverage data | - |")

    out += ["", "<sub>Coverage is informational only and never fails the build.</sub>"]
    with open(args.output, "w", encoding="utf-8") as f:
        f.write("\n".join(out) + "\n")


if __name__ == "__main__":
    main()
