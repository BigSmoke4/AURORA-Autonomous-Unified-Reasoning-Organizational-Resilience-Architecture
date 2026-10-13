"""Summarizes TRX test results for the CI run.

Emits GitHub workflow annotations (visible through the checks API) for every failed test,
and a Markdown summary for the job page. Exits non-zero when any test failed or when no
results were produced, so a silently empty run can never pass.
"""
import glob
import os
import sys
import xml.etree.ElementTree as ET

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def main() -> int:
    trx_files = glob.glob("TestResults/**/*.trx", recursive=True)
    if not trx_files:
        print("::error::No TRX test result files were produced; the test suite did not run.")
        return 1

    total = passed = failed = skipped = 0
    failures: list[str] = []
    for path in trx_files:
        root = ET.parse(path).getroot()
        for result in root.iter("{%s}UnitTestResult" % NS["t"]):
            total += 1
            outcome = result.get("outcome", "")
            name = result.get("testName", "unknown")
            if outcome == "Passed":
                passed += 1
            elif outcome == "NotExecuted":
                skipped += 1
            else:
                failed += 1
                message_el = result.find(".//t:Message", NS)
                message = (message_el.text or "").strip() if message_el is not None else outcome
                message = " ".join(message.split())[:500]
                failures.append(f"{name}: {message}")
                print(f"::error title=Test failed::{name} :: {message}")

    print(f"::notice title=Test results::{total} total, {passed} passed, {failed} failed, {skipped} not executed")
    if total == 0:
        print("::error::TRX files contained zero test results.")
        return 1

    summary_path = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary_path:
        with open(summary_path, "a", encoding="utf-8") as summary:
            summary.write("## Test results\n\n")
            summary.write(f"| Total | Passed | Failed | Not executed |\n|---|---|---|---|\n")
            summary.write(f"| {total} | {passed} | {failed} | {skipped} |\n\n")
            for line in failures:
                summary.write(f"- {line}\n")

    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
