# Claude Code Game Studios Upstream

- Source: https://github.com/Donchitos/Claude-Code-Game-Studios
- Installed from: `main` branch archive
- Installed on: 2026-08-04
- License: MIT, preserved in `third-party/Claude-Code-Game-Studios.LICENSE`

## Local Integration

- The root template `CLAUDE.md` is stored as `.claude/CLAUDE-STUDIOS.md` and
  imported by the existing repository `CLAUDE.md`.
- Engine settings are pinned to Unity 6000.3.21f1 and the existing
  ET/UGF/HybridCLR/Luban architecture.
- Template `docs/` and `production/session-state/` are installed project-local.
- Template `design/` content is merged into the existing `Design/` directory to
  avoid a case-insensitive Windows path collision.
- Generic `src/` and `.github/` template content is intentionally not copied;
  this brownfield project keeps its established repository layout.

Use `UPGRADING.md` from the upstream repository when updating, and merge rather
than overwrite the local integration files listed above.
