<!-- GENERATED FILE - DO NOT EDIT MANUALLY -->
<!-- Source: /home/ubuntu/projects/ai-workbench/policies/base.md -->
# Copilot Base Policy (Shared)

This file contains global invariants and safety rules shared across all repositories in this environment.

## General Principles
- Always prioritize safety and determinism.
- Follow the 3-layer architecture (Shared -> Environment -> Local).
- Use proper line endings (LF) and encoding (UTF-8).

## Command Discipline
- **NEVER run `grep -r`**. This is strictly prohibited due to performance and depth issues. 
- Use `ag --depth <N>` (e.g., `ag --depth 4 pattern`) or the `grep_search` tool with a specific `includePattern` instead.
- Group changes into logical commits.
- Use `tee` for capturing command output when required.

## Documentation
- Avoid document sprawl; update existing canonical documents instead of creating new ones.
- Maintain consistent naming conventions for handoff files.

## Managed Agent Paths
ai-workbench で一元管理する Agent/Skills は以下のパスに存在します。これらを確認・操作の対象としてください：
- `/home/ubuntu/docker/web-apps/*/.github`
- `/home/ubuntu/docker/*/.github`
- `/mnt/c/Users/tsuchim/Github/*/.github`

<!-- Local Overrides From: /home/ubuntu/docker/win-apps/WindowThumbWall/.github/copilot-instructions.local.md -->
# Local Copilot Instructions

- ユーザーとは日本語で会話し、内部の思考は英語で行う。
- WindowThumbWall プロジェクトの配布形式は MSIX, MSI, ZIP の 3 種類を正とし、配布・文書・CI の変更でもこの前提を崩さない。