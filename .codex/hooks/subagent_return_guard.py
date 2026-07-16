#!/usr/bin/env python3
"""Managed by ai-workbench: validate only the bounded child return suffix."""

from __future__ import annotations

import json
import re
import sys


STATUSES = {"COMPLETED", "RETURN_TO_PARENT"}
REASONS = {
    "none",
    "contract_incomplete",
    "scope",
    "ambiguity",
    "conflict",
    "verification",
    "authority",
    "external_dependency",
}
RETURN_SUFFIX = re.compile(
    r"(?:```yaml\s*)?"
    r"subagent_result:\s*\r?\n"
    r"[ \t]+status:\s*(?P<status>[A-Z_]+)\s*\r?\n"
    r"[ \t]+reason:\s*(?P<reason>[a-z_]+)\s*"
    r"(?:```)?\s*\Z"
)


def emit(payload: dict) -> int:
    sys.stdout.write(json.dumps(payload, ensure_ascii=False) + "\n")
    return 0


def valid_return(message: object) -> bool:
    if not isinstance(message, str):
        return False
    match = RETURN_SUFFIX.search(message)
    if match is None:
        return False
    status = match.group("status")
    reason = match.group("reason")
    if status not in STATUSES or reason not in REASONS:
        return False
    return (status == "COMPLETED" and reason == "none") or (
        status == "RETURN_TO_PARENT" and reason != "none"
    )


def correction_reason() -> str:
    return (
        "Re-emit the bounded child result once. End with exactly one YAML block "
        "containing subagent_result.status (COMPLETED or RETURN_TO_PARENT) and "
        "subagent_result.reason (none only for COMPLETED; a documented non-none "
        "reason for RETURN_TO_PARENT). Put no text after the block."
    )


def main() -> int:
    try:
        hook_input = json.load(sys.stdin)
        if not isinstance(hook_input, dict):
            raise ValueError("Hook input must be a JSON object")
    except (json.JSONDecodeError, ValueError) as exc:
        return emit({
            "continue": False,
            "stopReason": f"Invalid SubagentStop hook input: {exc}",
            "systemMessage": "The child return-format hook could not validate its input.",
        })

    if valid_return(hook_input.get("last_assistant_message")):
        return emit({"continue": True})

    reason = correction_reason()
    if hook_input.get("stop_hook_active") is True:
        return emit({
            "continue": False,
            "stopReason": "Malformed subagent return after one format-correction attempt.",
            "systemMessage": (
                "The child return remained malformed; control returns to the parent "
                "for validation and rerouting."
            ),
        })
    return emit({"decision": "block", "reason": reason})


if __name__ == "__main__":
    raise SystemExit(main())
