#!/usr/bin/env python3
"""Managed by ai-workbench: guard explicit durable execute objective closure."""

from __future__ import annotations

import json
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path

import yaml


STATE_RELATIVE = Path("ai-workbench") / "active-objective.json"
VIOLATIONS_RELATIVE = Path("ai-workbench") / "continuation-violations.jsonl"
ACTIVE = "active"


def emit(payload: dict) -> int:
    sys.stdout.write(json.dumps(payload, ensure_ascii=False) + "\n")
    return 0


def git_dir(cwd: Path) -> Path | None:
    result = subprocess.run(
        ["git", "rev-parse", "--path-format=absolute", "--git-dir"],
        cwd=cwd,
        check=False,
        text=True,
        capture_output=True,
    )
    return Path(result.stdout.strip()) if result.returncode == 0 else None


def read_json(path: Path) -> dict:
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, dict):
        raise ValueError(f"Expected JSON object: {path}")
    return data


def front_matter(path: Path) -> dict:
    text = path.read_text(encoding="utf-8")
    if not text.startswith("---\n"):
        raise ValueError(f"Missing YAML front matter: {path}")
    closing = text.find("\n---\n", 4)
    if closing == -1:
        raise ValueError(f"Unclosed YAML front matter: {path}")
    values = yaml.safe_load(text[4:closing])
    if not isinstance(values, dict):
        raise ValueError(f"Expected mapping front matter: {path}")
    return values


def record_violation(git_path: Path, hook_input: dict, binding: dict, reason: str) -> None:
    destination = git_path / VIOLATIONS_RELATIVE
    destination.parent.mkdir(parents=True, exist_ok=True)
    record = {
        "recorded_at": datetime.now(timezone.utc).isoformat(),
        "event": "continuation_contract_violation",
        "reason": reason,
        "session_id": hook_input.get("session_id"),
        "turn_id": hook_input.get("turn_id"),
        "job_id": binding.get("job_id"),
        "plan_path": binding.get("plan_path"),
    }
    with destination.open("a", encoding="utf-8") as stream:
        stream.write(json.dumps(record, ensure_ascii=False) + "\n")


def reconcile_or_stop(
    git_path: Path,
    hook_input: dict,
    binding: dict,
    reason: str,
) -> int:
    if hook_input.get("stop_hook_active") is True:
        record_violation(git_path, hook_input, binding, reason)
        return emit({"continue": False, "stopReason": reason, "systemMessage": reason})
    return emit({
        "decision": "block",
        "reason": f"Reconcile durable objective state before stopping: {reason}",
    })


def main() -> int:
    try:
        hook_input = json.load(sys.stdin)
        if not isinstance(hook_input, dict):
            raise ValueError("Hook input must be a JSON object")
    except (json.JSONDecodeError, ValueError) as exc:
        return emit({"continue": False, "stopReason": f"Invalid Stop hook input: {exc}"})

    cwd = Path(str(hook_input.get("cwd") or Path.cwd()))
    git_path = git_dir(cwd)
    if git_path is None:
        return emit({})
    pointer = git_path / STATE_RELATIVE
    if not pointer.is_file():
        return emit({})

    try:
        binding = read_json(pointer)
    except (OSError, ValueError, json.JSONDecodeError) as exc:
        return reconcile_or_stop(git_path, hook_input, {}, f"invalid active-objective binding: {exc}")

    if binding.get("schema_version") != 2:
        return reconcile_or_stop(git_path, hook_input, binding, "unsupported binding schema")
    if binding.get("mode") != "execute":
        return emit({})

    owner = str(binding.get("owner_session_id") or "")
    session = str(hook_input.get("session_id") or "")
    if owner and session and owner != session:
        return emit({})

    plan_path = Path(str(binding.get("plan_path") or ""))
    if not plan_path.is_file():
        return reconcile_or_stop(git_path, hook_input, binding, "bound plan is missing")
    try:
        state = front_matter(plan_path)
    except (OSError, ValueError) as exc:
        return reconcile_or_stop(git_path, hook_input, binding, f"invalid bound plan: {exc}")

    objective_status = state.get("objective_status")
    if objective_status in {"complete", "aborted"}:
        return emit({})
    if state.get("user_paused_or_cancelled") is True:
        return emit({})
    if objective_status != ACTIVE:
        return reconcile_or_stop(
            git_path,
            hook_input,
            binding,
            f"state drift: objective_status={objective_status!r}",
        )
    fields = ("runnable_route_count", "blocked_operation_count")
    if any(not isinstance(state.get(field), int) or state[field] < 0 for field in fields):
        return reconcile_or_stop(git_path, hook_input, binding, "invalid objective route counts")
    for field in ("operator_decision_required", "hard_external_blocker"):
        if not isinstance(state.get(field), bool):
            return reconcile_or_stop(git_path, hook_input, binding, f"invalid {field}")
    runnable = state["runnable_route_count"]
    if runnable == 0 and (state["operator_decision_required"] or state["hard_external_blocker"]):
        return emit({})
    if runnable == 0:
        return reconcile_or_stop(
            git_path, hook_input, binding,
            "zero runnable routes without a material decision or hard external blocker",
        )
    if hook_input.get("stop_hook_active") is True:
        reason = "Stop hook already continued this turn while the durable objective still reports runnable routes"
        record_violation(git_path, hook_input, binding, reason)
        return emit({"continue": False, "stopReason": reason, "systemMessage": reason})

    next_action = str(binding.get("next_action") or "").strip()
    if not next_action:
        return reconcile_or_stop(git_path, hook_input, binding, "binding has no explicit next action")
    return emit({
        "decision": "block",
        "reason": (
            f"Continue durable objective {binding.get('job_id')}. "
            f"Plan: {plan_path}. Next action: {next_action}"
        ),
    })


if __name__ == "__main__":
    sys.exit(main())
