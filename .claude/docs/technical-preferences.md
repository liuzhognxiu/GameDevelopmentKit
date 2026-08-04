# Technical Preferences

<!-- Brownfield baseline for GameDevelopmentKit. Update only after verifying the current project. -->

## Engine & Language

- **Engine**: Unity 6000.3.21f1
- **Language**: C#; .NET 8 for server and tooling projects
- **Rendering**: Preserve the active project render pipeline and inspect ProjectSettings before proposing changes
- **Physics**: Use the existing Unity Physics/Physics2D setup selected by each feature

## Input & Platform

<!-- Written by /setup-engine. Read by /ux-design, /ux-review, /test-setup, /team-ui, and /dev-story -->
<!-- to scope interaction specs, test helpers, and implementation to the correct input methods. -->

- **Target Platforms**: Product-specific; Windows Editor is the local validation baseline
- **Input Methods**: Inspect the active game module before choosing an input API
- **Primary Input**: Product-specific and must be recorded in the relevant design document
- **Gamepad Support**: Not assumed; verify per game
- **Touch Support**: Not assumed; verify per game
- **Platform Notes**: Do not change platform settings without reviewing build profiles and resource variants

## Naming Conventions

- **Classes**: PascalCase; ET analyzers and neighboring modules are authoritative
- **Variables**: camelCase locals; serialized instance fields normally use `m_CamelCase`
- **Signals/Events**: Follow UGF EventId and ET Event/System patterns already used by the owning module
- **Files**: Match the primary type and preserve Hot `Code/` versus non-hot `Loader/` boundaries
- **Scenes/Prefabs**: Follow existing resource directories and configuration-generated asset paths
- **Constants**: Follow local analyzer output and neighboring code; do not edit generated ID constants

## Performance Budgets

- **Target Framerate**: Define per product; do not invent a target during framework work
- **Frame Budget**: Derive from the approved target framerate and verify with Unity Profiler
- **Draw Calls**: Establish per scene/UI workload and verify with Frame Debugger
- **Memory Ceiling**: Establish per target platform and verify with Memory Profiler

## Testing

- **Framework**: Unity Test Framework, .NET test projects, ET analyzers, and repository validation scripts
- **Minimum Coverage**: Risk-based; lifecycle, generated-code boundaries, and cross-module contracts require focused regression tests
- **Required Tests**: Balance formulas, gameplay systems, networking (if applicable)

## Forbidden Patterns

<!-- Add patterns that should never appear in this project's codebase -->
- Direct editing of Unity prefab/scene YAML; use Unity Agent Bridge and editor APIs
- Manual edits to Luban, Proto, CodeBind, or UGF generated outputs
- Bypassing ET/UGF bridges or moving hot-update behavior into non-hot Loader code without an explicit architecture decision
- Replacing the established UGF resource pipeline or UGUI stack with Addressables/UI Toolkit by default

## Allowed Libraries / Addons

<!-- Add approved third-party dependencies here -->
- Existing packages and libraries already committed under `Unity/`, `DotNet/`, and `Share/`
- New dependencies require repository fit, license, platform, hot-update, and build-pipeline review

## Architecture Decisions Log

<!-- Quick reference linking to full ADRs in docs/architecture/ -->
- Existing architecture is documented in `KnowledgeBase/`; new durable decisions may be recorded under `docs/architecture/`

## Engine Specialists

<!-- Written by /setup-engine when engine is configured. -->
<!-- Read by /code-review, /architecture-decision, /architecture-review, and team skills -->
<!-- to know which specialist to spawn for engine-specific validation. -->

- **Primary**: `unity-specialist`
- **Language/Code Specialist**: `lead-programmer` with `unity-specialist`
- **Shader Specialist**: `unity-shader-specialist`
- **UI Specialist**: `unity-ui-specialist`
- **Additional Specialists**: `unity-dots-specialist`, `unity-addressables-specialist` only when their subsystem is actually selected
- **Routing Notes**: UGF/ET/HybridCLR repository evidence overrides generic Unity recommendations

### File Extension Routing

<!-- Skills use this table to select the right specialist per file type. -->
<!-- Route unknown file types through the Primary specialist. -->

| File Extension / Type | Specialist to Spawn |
|-----------------------|---------------------|
| Game code (primary language) | unity-specialist + lead-programmer |
| Shader / material files | unity-shader-specialist |
| UI / screen files | unity-ui-specialist |
| Scene / prefab / level files | unity-specialist |
| Native extension / plugin files | technical-director |
| General architecture review | Primary |
