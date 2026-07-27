#!/usr/bin/env python3
"""Run a repeatable Graphify-vs-focused-search pilot comparison."""

from __future__ import annotations

import argparse
import json
import math
import re
import statistics
import subprocess
import time
from datetime import datetime
from pathlib import Path


CSHARP_CASES = [
    {"id": "01", "action": "Explain", "source": "DefeatRemainingEnemiesForStageTransition"},
    {"id": "02", "action": "Explain", "source": "UpdateFlagArea"},
    {"id": "03", "action": "Explain", "source": "AttractRemainingStageRewards"},
    {"id": "04", "action": "Explain", "source": "ProgressionStore"},
    {
        "id": "05",
        "action": "Path",
        "source": "ProgressionStore",
        "target": "LobbyScreen",
    },
    {
        "id": "06",
        "action": "Path",
        "source": "SteamAchievementService",
        "target": "SteamAchievementRuntime",
    },
    {
        "id": "07",
        "action": "Path",
        "source": "StageTransitionRoutine",
        "target": "BeginStage",
    },
    {
        "id": "08",
        "action": "Path",
        "source": "GameManager",
        "target": "EnemyController",
    },
    {"id": "09", "action": "Affected", "source": "AdvancedWeaponArea", "depth": 2},
    {
        "id": "10",
        "action": "Affected",
        "source": "HasRemainingStageTransitionEnemy",
        "depth": 2,
    },
    {
        "id": "11",
        "action": "Affected",
        "source": "SteamAchievementService",
        "depth": 2,
    },
    {"id": "12", "action": "Affected", "source": "LobbyScreen", "depth": 2},
]

POWERSHELL_CASES = [
    {
        "id": "PS01",
        "action": "Path",
        "source": "Invoke-FullGraphRefresh",
        "target": "Invoke-GraphifyCommand",
        "baseline_path": "Tools",
        "extension": "ps1",
    },
    {
        "id": "PS02",
        "action": "Path",
        "source": "Invoke-FullGraphRefresh",
        "target": "Assert-ClusteredGraph",
        "baseline_path": "Tools",
        "extension": "ps1",
    },
    {
        "id": "PS03",
        "action": "Affected",
        "source": "Get-GraphFreshness",
        "depth": 2,
        "baseline_path": "Tools",
        "extension": "ps1",
    },
    {
        "id": "PS04",
        "action": "Affected",
        "source": "Write-GraphifyUsageRecord",
        "depth": 2,
        "baseline_path": "Tools",
        "extension": "ps1",
    },
    {
        "id": "PS05",
        "action": "Affected",
        "source": "Assert-ClusteredGraph",
        "depth": 2,
        "baseline_path": "Tools",
        "extension": "ps1",
    },
]


def approximate_tokens(text: str) -> int:
    return math.ceil(len(text) / 4)


def baseline_capture_tokens(output: str) -> int:
    """Sum Safe-Command estimates for source snippets, excluding wrapper metadata."""
    return sum(
        int(value)
        for value in re.findall(
            r"^estimated_tokens:\s*(\d+)\s*$", output, flags=re.MULTILINE
        )
    )


def run_command(command: list[str], root: Path, timeout_seconds: int) -> dict:
    started = time.perf_counter()
    completed = subprocess.run(
        command,
        cwd=root,
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
        timeout=timeout_seconds,
        check=False,
    )
    elapsed_ms = round((time.perf_counter() - started) * 1000)
    output = completed.stdout
    if completed.stderr:
        output += ("\n" if output else "") + completed.stderr
    return {
        "command": command,
        "exit_code": completed.returncode,
        "elapsed_ms": elapsed_ms,
        "output": output,
    }


def graph_capture(output: str) -> str:
    matches = re.findall(r"^capture_path:\s*(.+?)\s*$", output, flags=re.MULTILINE)
    if not matches:
        return output
    capture_path = Path(matches[-1].strip())
    if not capture_path.is_file():
        return output
    return capture_path.read_text(encoding="utf-8", errors="replace")


def graph_command(case: dict, wrapper: Path) -> list[str]:
    command = [
        "rtk",
        "powershell.exe",
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        str(wrapper),
        "-Action",
        case["action"],
        "-UsageCategory",
        "evaluation",
        "-Source",
        case["source"],
    ]
    if case.get("target"):
        command.extend(["-Target", case["target"]])
    if case.get("depth"):
        command.extend(["-Depth", str(case["depth"])])
    return command


def baseline_command(
    symbol: str, wrapper: Path, baseline_path: str, extension: str
) -> list[str]:
    return [
        "rtk",
        "powershell.exe",
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        str(wrapper),
        "-Pattern",
        symbol,
        "-Path",
        baseline_path,
        "-TopFiles",
        "3",
        "-Context",
        "3",
        "-MaxMatchesPerFile",
        "1",
        "-Extension",
        extension,
        "-PrintOutput",
    ]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", type=Path)
    parser.add_argument(
        "--suite",
        choices=("csharp", "powershell", "all"),
        default="csharp",
    )
    args = parser.parse_args()

    root = Path(__file__).resolve().parents[2]
    graph_wrapper = root / "Tools" / "TokenUsage" / "safe-graphify-pilot.ps1"
    baseline_wrapper = root / "Tools" / "TokenUsage" / "focused-search.ps1"
    timestamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    output_dir = (
        args.output_dir.resolve()
        if args.output_dir
        else root / "TokenReports" / f"graphify-pilot-evaluation-{timestamp}"
    )
    output_dir.mkdir(parents=True, exist_ok=True)
    cases = {
        "csharp": CSHARP_CASES,
        "powershell": POWERSHELL_CASES,
        "all": CSHARP_CASES + POWERSHELL_CASES,
    }[args.suite]

    results = []
    for case in cases:
        graph = run_command(graph_command(case, graph_wrapper), root, 90)
        (output_dir / f"{case['id']}-graphify.txt").write_text(
            graph["output"], encoding="utf-8"
        )
        if graph["exit_code"] != 0:
            raise RuntimeError(
                f"Graphify case {case['id']} failed with exit code "
                f"{graph['exit_code']}: {output_dir}"
            )

        symbols = [case["source"]]
        if case.get("target"):
            symbols.append(case["target"])
        baseline_runs = []
        for symbol in symbols:
            baseline = run_command(
                baseline_command(
                    symbol,
                    baseline_wrapper,
                    case.get("baseline_path", "Assets/AreaSurvivors"),
                    case.get("extension", "cs"),
                ),
                root,
                60,
            )
            baseline_runs.append(baseline)
            if baseline["exit_code"] != 0:
                raise RuntimeError(
                    f"Baseline case {case['id']} ({symbol}) failed with exit code "
                    f"{baseline['exit_code']}: {output_dir}"
                )
        baseline_output = "\n".join(item["output"] for item in baseline_runs)
        (output_dir / f"{case['id']}-baseline.txt").write_text(
            baseline_output, encoding="utf-8"
        )

        graph_output = graph_capture(graph["output"])
        required_symbols = [case["source"]]
        if case.get("target"):
            required_symbols.append(case["target"])
        results.append(
            {
                **case,
                "graphify": {
                    "elapsed_ms": graph["elapsed_ms"],
                    "chars": len(graph_output),
                    "estimated_tokens": approximate_tokens(graph_output),
                    "contains_required_symbols": all(
                        symbol in graph_output for symbol in required_symbols
                    ),
                },
                "baseline": {
                    "elapsed_ms": sum(item["elapsed_ms"] for item in baseline_runs),
                    "estimated_tokens": baseline_capture_tokens(baseline_output),
                    "contains_required_symbols": all(
                        symbol in baseline_output for symbol in required_symbols
                    ),
                    "invocations": len(baseline_runs),
                },
            }
        )

    graph_tokens = [item["graphify"]["estimated_tokens"] for item in results]
    baseline_tokens = [item["baseline"]["estimated_tokens"] for item in results]
    graph_times = [item["graphify"]["elapsed_ms"] for item in results]
    baseline_times = [item["baseline"]["elapsed_ms"] for item in results]
    summary = {
        "generated_at": datetime.now().isoformat(timespec="seconds"),
        "suite": args.suite,
        "case_count": len(results),
        "method": (
            "Graphify exact-symbol Explain/Path/Affected versus focused-search "
            "(top 3 files, context 3, one match per file). Path baselines search "
            "both endpoints separately. Token counts compare Graphify raw result "
            "content with the sum of safe-read captured-content estimates; wrapper "
            "commands and metadata are excluded."
        ),
        "graphify": {
            "total_estimated_tokens": sum(graph_tokens),
            "median_estimated_tokens": round(statistics.median(graph_tokens)),
            "total_elapsed_ms": sum(graph_times),
            "median_elapsed_ms": round(statistics.median(graph_times)),
            "required_symbol_successes": sum(
                item["graphify"]["contains_required_symbols"] for item in results
            ),
        },
        "baseline": {
            "total_estimated_tokens": sum(baseline_tokens),
            "median_estimated_tokens": round(statistics.median(baseline_tokens)),
            "total_elapsed_ms": sum(baseline_times),
            "median_elapsed_ms": round(statistics.median(baseline_times)),
            "required_symbol_successes": sum(
                item["baseline"]["contains_required_symbols"] for item in results
            ),
        },
        "ratios": {
            "token_reduction_percent": round(
                (1 - (sum(graph_tokens) / sum(baseline_tokens))) * 100, 1
            ),
            "elapsed_reduction_percent": round(
                (1 - (sum(graph_times) / sum(baseline_times))) * 100, 1
            ),
        },
        "results": results,
    }
    report_path = output_dir / "report.json"
    report_path.write_text(
        json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8"
    )
    print(
        json.dumps(
            {
                key: value
                for key, value in summary.items()
                if key not in {"results"}
            },
            ensure_ascii=False,
        )
    )
    print(f"report_path: {report_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
