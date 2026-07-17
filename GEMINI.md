<!-- Managed by ai-workbench -->
# GEMINI.md

このファイルは、Gemini 系エージェント向けの repo-level context entrypoint です。

@./AGENTS.md
@./.github/copilot-instructions.md

## Usage Notes

- まず import 済みの `AGENTS.md` と `.github/copilot-instructions.md` を project truth として扱ってください。
- 変更対象の path に応じて `.github/instructions/**/*.instructions.md` の該当ファイルを自分で開いて確認してください。
- `.gemini/config.yaml`、`.gemini/styleguide.md`、`.aiexclude` は Gemini / Google Code Assist の adapter surface です。shared policy の正本として再定義しないでください。
- remote host access は repo 内の名前や文書から推定せず、明示指示がある場合だけ扱ってください。