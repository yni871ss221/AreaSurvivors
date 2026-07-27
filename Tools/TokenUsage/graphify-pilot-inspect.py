import argparse
import json
import os
import sys
from collections import Counter
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--graph", required=True)
    parser.add_argument("--root", required=True)
    parser.add_argument("--allow-raw", action="store_true")
    parsed = parser.parse_args()

    graph_path = Path(parsed.graph).resolve()
    root_path = Path(parsed.root).resolve()
    if not graph_path.is_file():
        print(json.dumps({"ok": False, "error": "graph_missing", "graph": str(graph_path)}))
        return 2

    try:
        data = json.loads(graph_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        print(json.dumps({"ok": False, "error": "graph_unreadable", "detail": str(exc)}))
        return 3

    nodes = data.get("nodes")
    links = data.get("links")
    edges = data.get("edges")
    raw_schema = isinstance(nodes, list) and isinstance(edges, list) and not isinstance(links, list)
    clustered_schema = isinstance(nodes, list) and isinstance(links, list)
    if not clustered_schema and not (parsed.allow_raw and raw_schema):
        print(
            json.dumps(
                {
                    "ok": False,
                    "error": "raw_or_unknown_schema",
                    "has_nodes": isinstance(nodes, list),
                    "has_links": isinstance(links, list),
                    "has_edges": isinstance(edges, list),
                }
            )
        )
        return 4

    node_ids = [str(node.get("id", "")) for node in nodes]
    duplicate_ids = sorted(node_id for node_id, count in Counter(node_ids).items() if node_id and count > 1)
    absolute_sources = []
    outside_sources = []
    missing_source_files = 0

    for node in nodes:
        source_file = node.get("source_file")
        if not source_file:
            missing_source_files += 1
            continue
        source_text = str(source_file)
        if os.path.isabs(source_text):
            absolute_sources.append(source_text)
            resolved_source = Path(source_text).resolve()
            try:
                resolved_source.relative_to(root_path)
            except ValueError:
                outside_sources.append(source_text)

    result = {
        "ok": not duplicate_ids and not outside_sources,
        "schema": "raw-nodes-edges" if raw_schema else "networkx-node-link",
        "nodes": len(nodes),
        "links": len(edges) if raw_schema else len(links),
        "duplicate_node_ids": len(duplicate_ids),
        "absolute_source_files": len(set(absolute_sources)),
        "outside_source_files": len(set(outside_sources)),
        "nodes_without_source_file": missing_source_files,
        "graph_bytes": graph_path.stat().st_size,
        "graph": str(graph_path),
    }
    print(json.dumps(result, ensure_ascii=False))
    return 0 if result["ok"] else 5


if __name__ == "__main__":
    sys.exit(main())
