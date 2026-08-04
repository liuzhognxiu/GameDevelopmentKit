# Codex Compatibility Rules

The files under `workflows/`, `roles/`, `studio/`, and `standards/` are imported CCGS source material. Interpret them through this compatibility layer.

## Instruction Priority

1. System, developer, sandbox, and approval instructions active in Codex.
2. Scoped repository instructions such as `AGENTS.md`.
3. The user's current request and approved product decisions.
4. Current code, configuration, tests, and runtime evidence.
5. This adapter skill.
6. Imported CCGS workflow and role prompts.

Ignore an imported instruction when it conflicts with a higher-priority source.

## Tool Translation

| Claude CCGS term | Codex interpretation |
|---|---|
| `/workflow-name` | Invoke `$ccgs` and select `workflow-name`, or name it in natural language. |
| `AskUserQuestion` | Ask a concise direct question only when the decision is genuinely blocking. |
| `Task` / `subagent_type` | Use available Codex multi-agent orchestration, or perform role passes sequentially. |
| `Read`, `Glob`, `Grep` | Use Codex filesystem tools; prefer `rg` for search. |
| `Write`, `Edit` | Edit under Codex's active sandbox and repository rules. |
| `Bash` | Use the workspace shell and platform-native commands. |
| `model: opus/sonnet/haiku` | Ignore. Model routing belongs to Codex. |
| Claude hooks | Treat their intent as an explicit validation checklist; they are not Codex hooks. |

## Behavioral Translation

- Imported prompts often require approval before every write. In Codex, reserve questions for real product, architecture, destructive-operation, credential, or permission decisions. Implement normal requested changes autonomously.
- Imported prompts may assume a greenfield `src/` layout. Adapt to the actual engine and repository structure.
- Imported role opinions are advisory. Require source, runtime, test, design, or platform evidence for conclusions.
- Slash-command chains are workflow references, not commands to execute literally.
- Do not claim that a review, playtest, build, editor action, or platform check passed unless it actually ran.
- Treat imported `skill-test` and `skill-improve` as conceptual checklists only. Validate or update Codex skills with the active `skill-creator` workflow and Codex validators; do not edit imported reference copies as installed skills.

## Multi-Agent Contract

Give each worker:

- a non-overlapping scope;
- the relevant repository constraints;
- exact artifacts to inspect;
- the expected output format;
- a prohibition on unrelated edits;
- a request for file and line evidence.

Use an integration pass to deduplicate findings, resolve conflicts, and verify the final result.
