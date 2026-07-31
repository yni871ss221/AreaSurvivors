#!/usr/bin/env python3
"""Incremental SQLite index and exact query engine for TokenReports JSONL."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sqlite3
import sys
import tempfile
import time
from collections import defaultdict
from datetime import datetime, timedelta
from pathlib import Path
from typing import Any


INDEX_SCHEMA_VERSION = 2
PREFIX_BYTES = 4096
BOUNDARY_BYTES = 4096


def parse_datetime(value: str) -> datetime | None:
    text = (value or "").strip()
    if not text:
        return None
    try:
        normalized = text[:-1] + "+00:00" if text.endswith("Z") else text
        fractional = re.match(
            r"^(.*T\d{2}:\d{2}:\d{2})\.(\d+)(.*)$",
            normalized,
        )
        if fractional and len(fractional.group(2)) > 6:
            normalized = (
                fractional.group(1)
                + "."
                + fractional.group(2)[:6]
                + fractional.group(3)
            )
        parsed = datetime.fromisoformat(normalized)
        if parsed.tzinfo is None:
            parsed = parsed.astimezone()
        return parsed
    except ValueError:
        return None


def iso_timestamp(value: datetime) -> str:
    if value.tzinfo is None:
        value = value.astimezone()
    return value.isoformat(timespec="microseconds")


def numeric(value: Any, default: int = 0) -> int:
    if value is None or isinstance(value, bool):
        return default
    try:
        return int(value)
    except (TypeError, ValueError):
        return default


def nullable_numeric(value: Any) -> int | None:
    if value is None or isinstance(value, bool):
        return None
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def nullable_float(value: Any) -> float | None:
    if value is None or isinstance(value, bool):
        return None
    try:
        return float(value)
    except (TypeError, ValueError):
        return None


def nested(record: dict[str, Any], key: str) -> dict[str, Any]:
    value = record.get(key)
    return value if isinstance(value, dict) else {}


def normalize_record(
    raw: Any,
    source_path: str,
    source_offset: int,
    report_file: str,
) -> dict[str, Any] | None:
    record = raw if isinstance(raw, dict) else {}
    timestamp_text = str(record.get("timestamp") or "")
    timestamp_value = parse_datetime(timestamp_text)
    if timestamp_text and timestamp_value is None:
        return None

    kind = str(record.get("kind") or "")
    if (
        not kind
        and "graphify_version" in record
        and "action" in record
    ):
        kind = "graphify"
    if not kind:
        kind = "unrecognized_schema"

    schema_version = numeric(record.get("schema_version"), 0)
    estimate = nested(record, "estimate")
    visibility = nested(record, "visibility")
    capture_tokens = 0
    if estimate.get("estimated_tokens") is not None:
        capture_tokens = numeric(estimate.get("estimated_tokens"))
    elif kind == "graphify" and record.get("estimated_output_tokens") is not None:
        capture_tokens = numeric(record.get("estimated_output_tokens"))

    displayed_tokens: int | None = None
    measurement_status = "legacy_capture_only"
    if visibility.get("displayed_capture_estimated_tokens") is not None:
        displayed_tokens = numeric(
            visibility.get("displayed_capture_estimated_tokens")
        )
        measurement_status = "measured_display"
    elif (
        kind == "graphify"
        and record.get("displayed_estimated_tokens") is not None
    ):
        displayed_tokens = numeric(record.get("displayed_estimated_tokens"))
        measurement_status = "measured_display"
    elif kind == "safe_command" and schema_version >= 2:
        measurement_status = "current_schema_gap"

    command = str(record.get("command") or "")
    if not command and kind == "graphify":
        command = "Graphify {0} {1} {2}".format(
            record.get("action") or "",
            record.get("source") or "",
            record.get("target") or "",
        ).strip()

    coverage_start = nested(record, "coverage_start")
    return {
        "source_path": source_path,
        "source_offset": source_offset,
        "timestamp": timestamp_text,
        "timestamp_epoch": (
            timestamp_value.timestamp() if timestamp_value is not None else None
        ),
        "kind": kind,
        "command": command,
        "caller_script": str(record.get("caller_script") or ""),
        "area_tool_operation": str(record.get("area_tool_operation") or ""),
        "schema_version": schema_version,
        "tokens": capture_tokens,
        "capture_tokens": capture_tokens,
        "displayed_tokens": displayed_tokens,
        "measurement_status": measurement_status,
        "risk": str(estimate.get("risk") or ""),
        "blocked": bool(record.get("blocked")),
        "exit_code": nullable_numeric(record.get("exit_code")),
        "timeout_seconds": nullable_numeric(record.get("timeout_seconds")),
        "timed_out": bool(record.get("timed_out")),
        "capture_path": str(record.get("capture_path") or ""),
        "report_file": report_file,
        "coverage_start_ui_percent": (
            nullable_float(coverage_start.get("ui_percent"))
            if kind == "token_start_marker"
            else None
        ),
        "coverage_start_budget_tokens": (
            nullable_numeric(coverage_start.get("budget_tokens"))
            if kind == "token_start_marker"
            else None
        ),
        "coverage_start_note": (
            str(coverage_start.get("note") or "")
            if kind == "token_start_marker"
            else ""
        ),
    }


def parse_error_record(
    source_path: str,
    source_offset: int,
    report_file: str,
) -> dict[str, Any]:
    return {
        "source_path": source_path,
        "source_offset": source_offset,
        "timestamp": "",
        "timestamp_epoch": None,
        "kind": "parse_error",
        "command": report_file,
        "caller_script": "",
        "area_tool_operation": "",
        "schema_version": 0,
        "tokens": 0,
        "capture_tokens": 0,
        "displayed_tokens": None,
        "measurement_status": "parse_error",
        "risk": "unknown",
        "blocked": False,
        "exit_code": None,
        "timeout_seconds": None,
        "timed_out": False,
        "capture_path": "",
        "report_file": report_file,
        "coverage_start_ui_percent": None,
        "coverage_start_budget_tokens": None,
        "coverage_start_note": "",
    }


def initialize_database(connection: sqlite3.Connection) -> None:
    connection.execute("PRAGMA foreign_keys = ON")
    connection.execute("PRAGMA journal_mode = WAL")
    connection.execute("PRAGMA synchronous = NORMAL")
    connection.execute(
        """
        CREATE TABLE IF NOT EXISTS meta (
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL
        )
        """
    )
    row = connection.execute(
        "SELECT value FROM meta WHERE key = 'schema_version'"
    ).fetchone()
    if row is not None and numeric(row[0], -1) != INDEX_SCHEMA_VERSION:
        raise sqlite3.DatabaseError("token report index schema mismatch")
    connection.execute(
        "INSERT OR REPLACE INTO meta(key, value) VALUES('schema_version', ?)",
        (str(INDEX_SCHEMA_VERSION),),
    )
    connection.execute(
        """
        CREATE TABLE IF NOT EXISTS sources (
            path TEXT PRIMARY KEY,
            file_name TEXT NOT NULL,
            observed_length INTEGER NOT NULL,
            mtime_ns INTEGER NOT NULL,
            mtime_epoch REAL NOT NULL,
            prefix_length INTEGER NOT NULL,
            prefix_hash TEXT NOT NULL,
            processed_offset INTEGER NOT NULL,
            boundary_hash TEXT NOT NULL
        )
        """
    )
    connection.execute(
        """
        CREATE TABLE IF NOT EXISTS records (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            source_path TEXT NOT NULL,
            source_offset INTEGER NOT NULL,
            timestamp_text TEXT NOT NULL,
            timestamp_epoch REAL,
            kind TEXT NOT NULL,
            command_text TEXT NOT NULL,
            caller_script TEXT NOT NULL,
            area_tool_operation TEXT NOT NULL,
            schema_version INTEGER NOT NULL,
            capture_tokens INTEGER NOT NULL,
            displayed_tokens INTEGER,
            measurement_status TEXT NOT NULL,
            risk TEXT NOT NULL,
            blocked INTEGER NOT NULL,
            exit_code INTEGER,
            timeout_seconds INTEGER,
            timed_out INTEGER NOT NULL,
            capture_path TEXT NOT NULL,
            report_file TEXT NOT NULL,
            coverage_start_ui_percent REAL,
            coverage_start_budget_tokens INTEGER,
            coverage_start_note TEXT NOT NULL,
            UNIQUE(source_path, source_offset),
            FOREIGN KEY(source_path) REFERENCES sources(path) ON DELETE CASCADE
        )
        """
    )
    connection.execute(
        "CREATE INDEX IF NOT EXISTS idx_records_timestamp ON records(timestamp_epoch)"
    )
    connection.execute(
        "CREATE INDEX IF NOT EXISTS idx_records_kind ON records(kind)"
    )
    connection.commit()


def remove_database_files(path: Path) -> None:
    for candidate in (
        path,
        Path(str(path) + "-wal"),
        Path(str(path) + "-shm"),
    ):
        try:
            candidate.unlink()
        except FileNotFoundError:
            pass


def hash_region(path: Path, start: int, length: int) -> str:
    if length <= 0:
        return hashlib.sha256(b"").hexdigest()
    with path.open("rb") as stream:
        stream.seek(start)
        data = stream.read(length)
    return hashlib.sha256(data).hexdigest()


def boundary_hash(path: Path, offset: int) -> str:
    start = max(0, offset - BOUNDARY_BYTES)
    return hash_region(path, start, offset - start)


def insert_record(
    connection: sqlite3.Connection, record: dict[str, Any]
) -> None:
    values = (
        record["source_path"],
        record["source_offset"],
        record["timestamp"],
        record["timestamp_epoch"],
        record["kind"],
        record["command"],
        record["caller_script"],
        record["area_tool_operation"],
        record["schema_version"],
        record["capture_tokens"],
        record["displayed_tokens"],
        record["measurement_status"],
        record["risk"],
        int(record["blocked"]),
        record["exit_code"],
        record["timeout_seconds"],
        int(record["timed_out"]),
        record["capture_path"],
        record["report_file"],
        record["coverage_start_ui_percent"],
        record["coverage_start_budget_tokens"],
        record["coverage_start_note"],
    )
    connection.execute(
        """
        INSERT OR REPLACE INTO records(
            source_path, source_offset, timestamp_text, timestamp_epoch,
            kind, command_text, caller_script, area_tool_operation,
            schema_version, capture_tokens, displayed_tokens,
            measurement_status, risk, blocked, exit_code, timeout_seconds,
            timed_out, capture_path, report_file,
            coverage_start_ui_percent, coverage_start_budget_tokens,
            coverage_start_note
        ) VALUES(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
        """,
        values,
    )


def source_files(report_item: Path) -> tuple[list[Path], bool]:
    if report_item.is_file():
        return [report_item], True
    if not report_item.exists():
        return [], False
    return sorted(
        (
            path
            for path in report_item.iterdir()
            if path.is_file() and path.suffix.lower() == ".jsonl"
        ),
        key=lambda item: item.name.lower(),
    ), False


def update_index(
    connection: sqlite3.Connection,
    files: list[Path],
    force_rebuild: bool,
) -> dict[str, Any]:
    stats = {
        "rebuilt_files": 0,
        "appended_files": 0,
        "unchanged_files": 0,
        "removed_files": 0,
        "parsed_records": 0,
        "skipped_invalid_timestamps": 0,
    }
    current_paths = {str(path.resolve()) for path in files}

    connection.execute("BEGIN IMMEDIATE")
    try:
        existing_paths = {
            row[0] for row in connection.execute("SELECT path FROM sources")
        }
        for missing in existing_paths - current_paths:
            connection.execute("DELETE FROM sources WHERE path = ?", (missing,))
            stats["removed_files"] += 1

        for path in files:
            try:
                file_stat = path.stat()
            except FileNotFoundError:
                continue
            resolved = str(path.resolve())
            snapshot_size = file_stat.st_size
            previous = connection.execute(
                """
                SELECT observed_length, mtime_ns, prefix_length, prefix_hash,
                       processed_offset, boundary_hash
                FROM sources WHERE path = ?
                """,
                (resolved,),
            ).fetchone()

            rebuild = force_rebuild or previous is None
            offset = 0
            stored_prefix_length = 0
            if previous is not None and not rebuild:
                (
                    old_length,
                    old_mtime,
                    old_prefix_length,
                    old_prefix,
                    old_offset,
                    old_boundary,
                ) = previous
                offset = int(old_offset)
                stored_prefix_length = int(old_prefix_length)
                current_prefix = hash_region(
                    path,
                    0,
                    min(stored_prefix_length, snapshot_size),
                )
                if (
                    snapshot_size < int(old_length)
                    or snapshot_size < offset
                    or current_prefix != old_prefix
                    or (
                        snapshot_size == int(old_length)
                        and file_stat.st_mtime_ns != int(old_mtime)
                    )
                    or boundary_hash(path, offset) != old_boundary
                ):
                    rebuild = True
                    offset = 0
                    stored_prefix_length = 0

            if rebuild:
                connection.execute(
                    "DELETE FROM records WHERE source_path = ?", (resolved,)
                )
                stats["rebuilt_files"] += 1

            connection.execute(
                """
                INSERT OR IGNORE INTO sources(
                    path, file_name, observed_length, mtime_ns, mtime_epoch,
                    prefix_length, prefix_hash, processed_offset, boundary_hash
                ) VALUES(?,?,?,?,?,?,?,?,?)
                """,
                (
                    resolved,
                    path.name,
                    snapshot_size,
                    file_stat.st_mtime_ns,
                    file_stat.st_mtime,
                    stored_prefix_length,
                    hash_region(path, 0, stored_prefix_length),
                    offset,
                    boundary_hash(path, offset),
                ),
            )

            parsed_for_file = 0
            processed_offset = offset
            if snapshot_size > offset:
                with path.open("rb") as stream:
                    stream.seek(offset)
                    chunk = stream.read(snapshot_size - offset)
                last_newline = chunk.rfind(b"\n")
                if last_newline >= 0:
                    complete = chunk[: last_newline + 1]
                    position = offset
                    for raw_line in complete.splitlines(keepends=True):
                        line_offset = position
                        position += len(raw_line)
                        stripped = raw_line.rstrip(b"\r\n")
                        if not stripped.strip():
                            continue
                        try:
                            encoding = "utf-8-sig" if line_offset == 0 else "utf-8"
                            raw = json.loads(stripped.decode(encoding))
                            normalized = normalize_record(
                                raw, resolved, line_offset, path.name
                            )
                            if normalized is None:
                                stats["skipped_invalid_timestamps"] += 1
                                continue
                        except (UnicodeDecodeError, json.JSONDecodeError):
                            normalized = parse_error_record(
                                resolved, line_offset, path.name
                            )
                        insert_record(connection, normalized)
                        parsed_for_file += 1
                    processed_offset = offset + len(complete)

            if previous is not None and not rebuild:
                if parsed_for_file > 0:
                    stats["appended_files"] += 1
                else:
                    stats["unchanged_files"] += 1

            connection.execute(
                """
                INSERT INTO sources(
                    path, file_name, observed_length, mtime_ns, mtime_epoch,
                    prefix_length, prefix_hash, processed_offset, boundary_hash
                ) VALUES(?,?,?,?,?,?,?,?,?)
                ON CONFLICT(path) DO UPDATE SET
                    file_name = excluded.file_name,
                    observed_length = excluded.observed_length,
                    mtime_ns = excluded.mtime_ns,
                    mtime_epoch = excluded.mtime_epoch,
                    prefix_length = excluded.prefix_length,
                    prefix_hash = excluded.prefix_hash,
                    processed_offset = excluded.processed_offset,
                    boundary_hash = excluded.boundary_hash
                """,
                (
                    resolved,
                    path.name,
                    snapshot_size,
                    file_stat.st_mtime_ns,
                    file_stat.st_mtime,
                    (
                        stored_prefix_length
                        if stored_prefix_length > 0
                        else min(PREFIX_BYTES, processed_offset)
                    ),
                    hash_region(
                        path,
                        0,
                        (
                            stored_prefix_length
                            if stored_prefix_length > 0
                            else min(PREFIX_BYTES, processed_offset)
                        ),
                    ),
                    processed_offset,
                    boundary_hash(path, processed_offset),
                ),
            )
            stats["parsed_records"] += parsed_for_file
        connection.commit()
    except Exception:
        connection.rollback()
        raise
    return stats


def row_to_record(row: sqlite3.Row) -> dict[str, Any]:
    return {
        "timestamp": row["timestamp_text"],
        "timestamp_epoch": row["timestamp_epoch"],
        "kind": row["kind"],
        "command": row["command_text"],
        "caller_script": row["caller_script"],
        "area_tool_operation": row["area_tool_operation"],
        "schema_version": row["schema_version"],
        "tokens": row["capture_tokens"],
        "capture_tokens": row["capture_tokens"],
        "displayed_tokens": row["displayed_tokens"],
        "measurement_status": row["measurement_status"],
        "risk": row["risk"],
        "blocked": bool(row["blocked"]),
        "exit_code": row["exit_code"],
        "timeout_seconds": row["timeout_seconds"],
        "timed_out": bool(row["timed_out"]),
        "capture_path": row["capture_path"],
        "report_file": row["report_file"],
        "coverage_start_ui_percent": row["coverage_start_ui_percent"],
        "coverage_start_budget_tokens": row[
            "coverage_start_budget_tokens"
        ],
        "coverage_start_note": row["coverage_start_note"],
    }


def failed(record: dict[str, Any]) -> bool:
    return (
        record["kind"] == "parse_error"
        or record["timed_out"]
        or (
            record["exit_code"] is not None
            and numeric(record["exit_code"]) != 0
        )
    )


def summarize(
    connection: sqlite3.Connection,
    report_item: Path,
    explicit_file: bool,
    days: int,
    since_text: str,
    kinds: list[str],
    top: int,
    recent: int,
    since_last_start: bool,
    failed_only: bool,
    include_benchmark: bool,
    index_stats: dict[str, Any],
    cache_path: Path,
    index_elapsed_ms: int,
) -> dict[str, Any]:
    if since_text.strip():
        since_date = parse_datetime(since_text)
        if since_date is None:
            raise ValueError(f"Invalid -Since value: {since_text}")
    else:
        today = datetime.now().astimezone().replace(
            hour=0, minute=0, second=0, microsecond=0
        )
        since_date = today - timedelta(days=max(0, days - 1))
    since_epoch = since_date.timestamp()

    where = ["(r.timestamp_epoch IS NULL OR r.timestamp_epoch >= ?)"]
    parameters: list[Any] = [since_epoch]
    if not explicit_file:
        where.append("s.mtime_epoch >= ?")
        parameters.append(since_epoch)
    query = f"""
        SELECT r.*, s.mtime_epoch
        FROM records r
        JOIN sources s ON s.path = r.source_path
        WHERE {' AND '.join(where)}
        ORDER BY r.id
    """
    records = [
        row_to_record(row)
        for row in connection.execute(query, tuple(parameters))
    ]

    latest_marker: dict[str, Any] | None = None
    if since_last_start:
        markers = [
            record
            for record in records
            if record["kind"] == "token_start_marker"
            and record["timestamp"]
        ]
        if markers:
            latest_marker = sorted(
                markers, key=lambda item: item["timestamp"]
            )[-1]
            marker_epoch = latest_marker["timestamp_epoch"]
            since_date = datetime.fromtimestamp(
                marker_epoch, tz=since_date.tzinfo
            )
            records = [
                record
                for record in records
                if record["timestamp_epoch"] is not None
                and record["timestamp_epoch"] >= marker_epoch
            ]

    if not include_benchmark:
        records = [
            record
            for record in records
            if record["kind"] not in ("benchmark", "token_start_marker")
        ]
    if kinds:
        kind_set = set(kinds)
        records = [
            record for record in records if record["kind"] in kind_set
        ]
    if failed_only:
        records = [record for record in records if failed(record)]
    if recent > 0:
        records = sorted(records, key=lambda item: item["timestamp"])[-recent:]

    total_capture = sum(record["capture_tokens"] for record in records)
    total_manual = sum(
        record["capture_tokens"]
        for record in records
        if record["kind"] == "manual_untracked_usage"
    )
    displayed_records = [
        record
        for record in records
        if record["measurement_status"] == "measured_display"
    ]
    total_displayed = sum(
        numeric(record["displayed_tokens"]) for record in displayed_records
    )
    capture_only = [
        record
        for record in records
        if record["measurement_status"] == "legacy_capture_only"
    ]

    grouped: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for record in records:
        grouped[record["kind"]].append(record)
    kind_summary = []
    for kind, items in grouped.items():
        kind_summary.append(
            {
                "kind": kind,
                "records": len(items),
                "tokens": sum(item["capture_tokens"] for item in items),
                "capture_tokens": sum(
                    item["capture_tokens"] for item in items
                ),
                "displayed_tokens": sum(
                    numeric(item["displayed_tokens"])
                    for item in items
                    if item["displayed_tokens"] is not None
                ),
                "measurement_gaps": sum(
                    1
                    for item in items
                    if item["measurement_status"] != "measured_display"
                ),
            }
        )
    kind_summary.sort(key=lambda item: item["capture_tokens"], reverse=True)

    top_commands = sorted(
        records,
        key=lambda item: (
            -1
            if item["displayed_tokens"] is None
            else numeric(item["displayed_tokens"]),
            item["capture_tokens"],
        ),
        reverse=True,
    )[: max(0, top)]
    for record in top_commands:
        record.pop("timestamp_epoch", None)

    operation_summary = []
    operations: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for record in records:
        if record["area_tool_operation"]:
            operations[record["area_tool_operation"]].append(record)
    for operation, items in operations.items():
        operation_summary.append(
            {
                "operation": operation,
                "records": len(items),
                "capture_tokens": sum(
                    item["capture_tokens"] for item in items
                ),
                "displayed_tokens": sum(
                    numeric(item["displayed_tokens"])
                    for item in items
                    if item["displayed_tokens"] is not None
                ),
            }
        )
    operation_summary.sort(
        key=lambda item: item["displayed_tokens"], reverse=True
    )

    return {
        "report_directory": str(
            (report_item.parent if explicit_file else report_item).resolve()
        ),
        "report_file": report_item.name if explicit_file else "",
        "days": days,
        "since": iso_timestamp(since_date),
        "kinds": kinds,
        "recent": recent,
        "since_last_start": bool(since_last_start),
        "failed_only": bool(failed_only),
        "records": len(records),
        "total_estimated_tokens": total_capture,
        "manual_recorded_estimated_tokens": total_manual,
        "total_capture_estimated_tokens": total_capture,
        "total_displayed_estimated_tokens": total_displayed,
        "displayed_measured_records": len(displayed_records),
        "capture_only_records": len(capture_only),
        "measurement_coverage_percent": (
            100.0
            if not records
            else round((len(displayed_records) / len(records)) * 100.0, 1)
        ),
        "latest_start_ui_percent": (
            latest_marker["coverage_start_ui_percent"]
            if latest_marker is not None
            else None
        ),
        "latest_start_budget_tokens": (
            latest_marker["coverage_start_budget_tokens"]
            if latest_marker is not None
            else None
        ),
        "latest_start_note": (
            latest_marker["coverage_start_note"]
            if latest_marker is not None
            else ""
        ),
        "blocked_count": sum(1 for record in records if record["blocked"]),
        "high_or_critical_count": sum(
            1
            for record in records
            if record["risk"] in ("high", "critical")
        ),
        "kind_summary": kind_summary,
        "top_commands": top_commands,
        "operation_summary": operation_summary,
        "index": {
            "schema_version": INDEX_SCHEMA_VERSION,
            "cache_path": str(cache_path),
            "elapsed_ms": index_elapsed_ms,
            **index_stats,
        },
    }


def cache_database_path(cache_root: Path, report_item: Path) -> Path:
    scope = str(report_item.resolve()).lower()
    digest = hashlib.sha256(scope.encode("utf-8")).hexdigest()[:16]
    return cache_root / f"{digest}.sqlite3"


def run_summary(
    args: argparse.Namespace,
    allow_recovery: bool = True,
) -> dict[str, Any]:
    report_item = Path(args.report_directory).resolve()
    if not report_item.exists():
        return {"message": "No TokenReports directory found."}
    files, explicit_file = source_files(report_item)
    cache_root = Path(args.cache_root).resolve()
    cache_root.mkdir(parents=True, exist_ok=True)
    database_path = cache_database_path(cache_root, report_item)
    if args.force_rebuild:
        remove_database_files(database_path)

    started = time.perf_counter()
    try:
        connection = sqlite3.connect(str(database_path), timeout=30.0)
        connection.row_factory = sqlite3.Row
        try:
            initialize_database(connection)
            stats = update_index(connection, files, False)
            elapsed_ms = round((time.perf_counter() - started) * 1000)
            return summarize(
                connection=connection,
                report_item=report_item,
                explicit_file=explicit_file,
                days=args.days,
                since_text=args.since,
                kinds=args.kind,
                top=args.top,
                recent=args.recent,
                since_last_start=args.since_last_start,
                failed_only=args.failed_only,
                include_benchmark=args.include_benchmark,
                index_stats=stats,
                cache_path=database_path,
                index_elapsed_ms=elapsed_ms,
            )
        finally:
            connection.close()
    except sqlite3.DatabaseError:
        if not allow_recovery:
            raise
        remove_database_files(database_path)
        result = run_summary(args, allow_recovery=False)
        if "index" in result:
            result["index"]["recovered_corrupt_cache"] = True
        return result


def make_test_record(
    timestamp: str,
    command: str,
    capture: int,
    displayed: int | None,
    exit_code: int = 0,
) -> dict[str, Any]:
    record: dict[str, Any] = {
        "timestamp": timestamp,
        "kind": "safe_command",
        "schema_version": 3,
        "command": command,
        "exit_code": exit_code,
        "estimate": {"estimated_tokens": capture, "risk": "low"},
    }
    if displayed is not None:
        record["visibility"] = {
            "displayed_capture_estimated_tokens": displayed
        }
    return record


def self_test() -> None:
    with tempfile.TemporaryDirectory(prefix="area-token-index-") as root:
        root_path = Path(root)
        reports = root_path / "TokenReports"
        cache = root_path / "Cache"
        reports.mkdir()
        now = datetime.now().astimezone()
        report = reports / f"{now:%Y-%m-%d}.jsonl"
        rows = [
            make_test_record(iso_timestamp(now), "one", 10, 4),
            {
                "timestamp": iso_timestamp(now + timedelta(seconds=1)),
                "kind": "token_start_marker",
                "coverage_start": {
                    "ui_percent": 50,
                    "budget_tokens": 1000,
                    "note": "self-test",
                },
            },
            {
                "timestamp": iso_timestamp(now + timedelta(seconds=2)),
                "graphify_version": 1,
                "action": "path",
                "source": "A",
                "target": "B",
                "estimated_output_tokens": 7,
                "displayed_estimated_tokens": 3,
            },
        ]
        report.write_text(
            "".join(json.dumps(row, separators=(",", ":")) + "\n" for row in rows),
            encoding="utf-8",
        )

        namespace = argparse.Namespace(
            report_directory=str(reports),
            cache_root=str(cache),
            days=1,
            since="",
            kind=[],
            top=10,
            recent=0,
            since_last_start=False,
            failed_only=False,
            include_benchmark=False,
            force_rebuild=False,
        )
        first = run_summary(namespace)
        if first["records"] != 2 or first["index"]["parsed_records"] != 3:
            raise AssertionError(
                "initial index build failed: "
                f"records={first['records']} "
                f"parsed={first['index']['parsed_records']}"
            )
        second = run_summary(namespace)
        if second["records"] != 2 or second["index"]["parsed_records"] != 0:
            raise AssertionError("unchanged index was not reused")

        appended = make_test_record(
            iso_timestamp(now + timedelta(seconds=3)), "two", 5, 2
        )
        with report.open("ab") as stream:
            stream.write(json.dumps(appended).encode("utf-8"))
        partial = run_summary(namespace)
        if partial["records"] != 2:
            raise AssertionError("partial final line was indexed")
        with report.open("ab") as stream:
            stream.write(b"\n")
        completed = run_summary(namespace)
        if (
            completed["records"] != 3
            or completed["index"]["parsed_records"] != 1
        ):
            raise AssertionError("completed appended line was not indexed")

        replacement = make_test_record(
            iso_timestamp(now + timedelta(seconds=4)), "replacement", 2, 1
        )
        report.write_text(json.dumps(replacement) + "\n", encoding="utf-8")
        rebuilt = run_summary(namespace)
        if rebuilt["records"] != 1 or rebuilt["index"]["rebuilt_files"] != 1:
            raise AssertionError("truncated source did not rebuild")

        report.unlink()
        removed = run_summary(namespace)
        if removed["records"] != 0 or removed["index"]["removed_files"] != 1:
            raise AssertionError("removed source remained indexed")

        report.write_text(json.dumps(replacement) + "\n", encoding="utf-8")
        restored = run_summary(namespace)
        database = Path(restored["index"]["cache_path"])
        remove_database_files(database)
        database.write_bytes(b"not-a-sqlite-database")
        recovered = run_summary(namespace)
        if not recovered["index"].get("recovered_corrupt_cache"):
            raise AssertionError("corrupt cache was not recovered")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser()
    parser.add_argument("--report-directory", default="TokenReports")
    parser.add_argument(
        "--cache-root",
        default=str(Path("Library") / "AreaAgentIndex" / "TokenReports"),
    )
    parser.add_argument("--days", type=int, default=7)
    parser.add_argument("--since", default="")
    parser.add_argument("--kind", action="append", default=[])
    parser.add_argument("--top", type=int, default=10)
    parser.add_argument("--recent", type=int, default=0)
    parser.add_argument("--since-last-start", action="store_true")
    parser.add_argument("--failed-only", action="store_true")
    parser.add_argument("--include-benchmark", action="store_true")
    parser.add_argument("--force-rebuild", action="store_true")
    parser.add_argument("--self-test", action="store_true")
    parser.add_argument("--output-json", default="")
    return parser


def main() -> int:
    args = build_parser().parse_args()
    if args.self_test:
        self_test()
        print("Get-TokenReportSummary self-test passed.")
        return 0
    expanded_kinds: list[str] = []
    for item in args.kind:
        for part in item.split(","):
            trimmed = part.strip()
            if trimmed and trimmed not in expanded_kinds:
                expanded_kinds.append(trimmed)
    args.kind = expanded_kinds
    result = run_summary(args)
    if args.output_json:
        output_path = Path(args.output_json)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(
            json.dumps(result, ensure_ascii=False, separators=(",", ":")),
            encoding="utf-8",
        )
    elif "message" in result:
        print(result["message"])
    else:
        print(json.dumps(result, ensure_ascii=False, separators=(",", ":")))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(f"token-report-index failed: {error}", file=sys.stderr)
        raise SystemExit(1)
