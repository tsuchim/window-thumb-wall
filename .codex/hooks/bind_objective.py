#!/usr/bin/env python3
"""Managed by ai-workbench: bind/clear explicit durable objective Git state."""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path


STATE_RELATIVE = Path("ai-workbench") / "active-objective.json"


def git_dir(cwd: Path) -> Path:
    result = subprocess.run(
        ["git", "rev-parse", "--path-format=absolute", "--git-dir"],
        cwd=cwd,
        check=True,
        text=True,
        capture_output=True,
    )
    return Path(result.stdout.strip())


def state_path(cwd: Path) -> Path:
    return git_dir(cwd) / STATE_RELATIVE


def bind(args: argparse.Namespace) -> int:
    plan = Path(args.plan_path).expanduser().resolve()
    if not plan.is_file():
        raise SystemExit(f"Plan file does not exist: {plan}")
    destination = state_path(Path.cwd())
    destination.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "schema_version": 2,
        "job_id": args.job_id,
        "plan_path": str(plan),
        "owner_session_id": args.owner_session_id,
        "mode": args.mode,
        "next_action": args.next_action,
    }
    destination.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    print(destination)
    return 0


def clear(_: argparse.Namespace) -> int:
    destination = state_path(Path.cwd())
    if destination.exists():
        destination.unlink()
    print(destination)
    return 0


def show(_: argparse.Namespace) -> int:
    destination = state_path(Path.cwd())
    if not destination.is_file():
        return 1
    sys.stdout.write(destination.read_text(encoding="utf-8"))
    return 0


def main() -> int:
    parser = argparse.ArgumentParser()
    subparsers = parser.add_subparsers(dest="command", required=True)

    bind_parser = subparsers.add_parser("bind")
    bind_parser.add_argument("--job-id", required=True)
    bind_parser.add_argument("--plan-path", required=True)
    bind_parser.add_argument("--owner-session-id", default="")
    bind_parser.add_argument("--mode", choices=("answer", "audit", "execute"), default="execute")
    bind_parser.add_argument("--next-action", required=True)
    bind_parser.set_defaults(handler=bind)

    clear_parser = subparsers.add_parser("clear")
    clear_parser.set_defaults(handler=clear)

    show_parser = subparsers.add_parser("show")
    show_parser.set_defaults(handler=show)

    args = parser.parse_args()
    return args.handler(args)


if __name__ == "__main__":
    sys.exit(main())
