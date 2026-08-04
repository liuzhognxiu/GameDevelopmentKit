# Directory Structure

This is a brownfield Unity framework. Do not create the template's generic
`src/` or `assets/` trees. Preserve the repository's established ownership
boundaries:

```text
/
|-- CLAUDE.md                    # Repository and Unity Agent Bridge guidance
|-- AGENTS.md                    # Shared agent guidance
|-- .claude/                     # Game Studios agents, skills, hooks, and rules
|-- Unity/                       # Unity 6000.3 project
|   |-- Assets/Scripts/Game/     # Game, Hot, ET integration, UI, procedures
|   |-- Assets/Scripts/Library/  # ET, UGF, UniTask, and extension libraries
|   `-- Assets/Res/              # Runtime resources and prefabs
|-- DotNet/                      # ET server-side .NET projects
|-- Share/                       # Shared analyzers, libraries, and services
|-- Tools/                       # Luban, configuration, and build tooling
|-- Design/                      # Existing Excel/config design sources
|   `-- gdd/                     # Game Studios design docs when created
|-- Book/                        # Human-authored framework documentation
|-- KnowledgeBase/               # AI-oriented verified repository knowledge
|-- docs/                        # Game Studios architecture and engine references
`-- production/                  # Studio stage, sprint, and session state
```

On Windows, template paths written as `design/...` resolve to the existing
`Design/` directory. Keep CCGS design documents under `Design/gdd` and never
move or rename the existing Excel/configuration sources.

