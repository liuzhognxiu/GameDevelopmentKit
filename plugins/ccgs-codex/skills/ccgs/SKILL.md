---
name: ccgs
description: Codex-native game studio workflow orchestration adapted from Claude Code Game Studios. Use for game project onboarding, phase detection, design and architecture gates, story implementation, specialist reviews, QA, release work, UI teams, or when the user names CCGS or a CCGS workflow.
---

# CCGS for Codex

Use CCGS as a workflow and role library. Do not treat the imported Claude prompts as executable tool instructions.

## Start Here

1. Read the repository's active `AGENTS.md` and any scoped instructions first.
2. Read `references/compatibility.md` before using an imported workflow.
3. Read `references/workflow-index.md` to select the smallest relevant workflow.
4. Load only the selected file from `references/workflows/`.
5. Load `references/role-index.md` and only the role prompts needed for the task.
6. Read supporting files under `references/studio/` or `references/standards/` only when the selected workflow points to them.

Repository instructions, actual source code, and runtime evidence take precedence over generic CCGS advice.

## Route The Request

Choose one primary workflow:

- New or adopted project: `start`, `adopt`, `onboard`, or `reverse-document`.
- Unsure what comes next: `help` or `project-stage-detect`.
- Concept and design: `brainstorm`, `quick-design`, `design-review`, `create-epics`, or `create-stories`.
- Architecture and setup: `create-architecture`, `architecture-decision`, `setup-engine`, or `test-setup`.
- Implementation: `sprint-plan`, `dev-story`, `story-done`, `prototype`, or a `team-*` workflow.
- Review and QA: `code-review`, `architecture-review`, `qa-plan`, `smoke-check`, `regression-suite`, or `test-evidence-review`.
- Shipping and operations: `release-checklist`, `launch-checklist`, `hotfix`, `day-one-patch`, or `team-live-ops`.

When a user names a workflow, use that workflow directly unless it conflicts with a higher-priority repository instruction.

## Execute In Codex

1. Establish the current project phase from real artifacts and source state.
2. State assumptions only when they materially affect implementation.
3. Identify decision gates. Ask the user only for product or architecture decisions that cannot be inferred safely.
4. For ordinary implementation requests, continue through edits and verification under Codex's active approval policy.
5. For reviews, report findings first, ordered by severity and grounded in file and line references.
6. For implementation, verify using the project's real build, tests, runtime, or editor bridge.
7. Update production artifacts only when the chosen workflow calls for them and they match the repository's conventions.

Do not invent completion evidence. Label unrun checks, unavailable tools, and subjective judgments clearly.

## Specialist Collaboration

Use specialist roles as focused review lenses, not as fictional authority.

- When multi-agent tools are available and the task benefits from parallel work, delegate independent scopes with explicit inputs, outputs, and ownership boundaries.
- When multi-agent tools are unavailable, perform the same role passes sequentially and keep conclusions evidence-based.
- Resolve disagreements against repository constraints, product goals, source evidence, and test results.
- Do not copy Claude model names, `Task` syntax, `subagent_type`, or tool allowlists into delegation prompts.

For Unity work, prefer the repository's Unity specialist roles and obey any Agent Bridge instructions in `AGENTS.md` before querying or changing the Editor.

## CCGS Artifacts

CCGS commonly uses `docs/`, `design/` or `Design/`, and `production/`. Preserve the repository's existing casing and layout. Never create a generic `src/` tree in an established engine project merely because an imported prompt assumes one.

Use the source workflow as a checklist, then adapt paths, commands, and outputs to the current codebase.

