#!/usr/bin/env python3
"""Run the isolated Lunar stalled-repository A/B regression using explicit tools and inputs."""

from __future__ import annotations

import argparse
import json
import pathlib
import subprocess
import sys
import time


def existing_file(value: str) -> pathlib.Path:
    path = pathlib.Path(value).expanduser().resolve()
    if not path.is_file():
        raise argparse.ArgumentTypeError(f"file does not exist: {path}")
    return path


def run(command: list[str], timeout: int) -> subprocess.CompletedProcess[str]:
    return subprocess.run(command, text=True, capture_output=True, timeout=timeout, check=False)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--java", required=True, type=existing_file, help="Java 17 runtime used to execute the regression")
    parser.add_argument("--compiler-java", required=True, type=existing_file, help="Java runtime used to invoke ECJ")
    parser.add_argument("--ecj", required=True, type=existing_file, help="Eclipse Compiler for Java jar")
    parser.add_argument("--original", required=True, type=existing_file, help="original Genesis jar")
    parser.add_argument("--patched", required=True, type=existing_file, help="candidate Genesis jar")
    parser.add_argument("--scratch", required=True, type=pathlib.Path, help="scratch directory for compiled classes and summary")
    parser.add_argument("--summary", type=pathlib.Path, help="optional JSON result summary path (defaults inside scratch)")
    args = parser.parse_args()

    scratch = args.scratch.expanduser().resolve()
    classes = scratch / "stalled-fallback-classes"
    try:
        scratch.mkdir(parents=True, exist_ok=True)
        classes.mkdir(parents=True, exist_ok=True)
    except OSError as error:
        print(f"FAIL cannot create scratch directory: {error}", file=sys.stderr)
        return 2

    source = pathlib.Path(__file__).resolve().with_name("StalledFallbackTest.java")
    if not source.is_file():
        print("FAIL regression Java source is missing", file=sys.stderr)
        return 2

    records: list[dict[str, object]] = []
    started = time.monotonic()
    compile_command = [str(args.compiler_java), "-jar", str(args.ecj), "-17", "-d", str(classes), str(source)]
    try:
        compiled = run(compile_command, 30)
    except subprocess.TimeoutExpired:
        print("FAIL Java compilation exceeded 30 seconds", file=sys.stderr)
        return 1
    if compiled.returncode:
        print("FAIL Java compilation", file=sys.stderr)
        if compiled.stderr:
            print(compiled.stderr[-3000:], file=sys.stderr)
        return 1

    for mode, jar in (("original", args.original), ("candidate", args.patched)):
        command = [str(args.java), "-Xverify:all", "--add-modules", "java.net.http,jdk.httpserver", "-cp", str(classes), "StalledFallbackTest", mode, str(jar)]
        try:
            result = run(command, 34)
        except subprocess.TimeoutExpired:
            print(f"FAIL {mode} probe exceeded the 34-second parent limit")
            return 1
        output = result.stdout.strip()
        if output:
            print(output)
        records.append({"mode": mode, "exitCode": result.returncode, "output": output})
        if result.returncode:
            print(f"FAIL {mode} probe exited with code {result.returncode}")
            if result.stderr:
                print(result.stderr[-1000:].replace("\n", " "), file=sys.stderr)
            return 1

    elapsed = round(time.monotonic() - started, 3)
    summary = {
        "result": "PASS",
        "test": "isolated bounded TEST-NET stalled-connect A/B regression",
        "elapsedSeconds": elapsed,
        "checks": records,
        "scope": "scratch-only compilation; localhost fixture; no live Lunar files or launcher state",
    }
    summary_path = (args.summary or (scratch / "stalled-fallback-summary.json")).expanduser().resolve()
    try:
        summary_path.relative_to(scratch)
    except ValueError:
        print("FAIL summary path must be inside scratch", file=sys.stderr)
        return 2
    try:
        summary_path.parent.mkdir(parents=True, exist_ok=True)
        summary_path.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    except OSError as error:
        print(f"FAIL cannot write result summary: {error}", file=sys.stderr)
        return 2
    print("PASS bounded isolated A/B regression; TEST-NET timeout and loopback fallback; no live-client state used")
    print(f"summary={summary_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
