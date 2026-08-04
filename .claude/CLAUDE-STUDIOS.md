# Claude Code Game Studios -- GameDevelopmentKit Integration

Indie game development managed through 49 coordinated Claude Code subagents.
Each agent owns a specific domain, enforcing separation of concerns and quality.

## Technology Stack

- **Engine**: Unity 6000.3.21f1
- **Language**: C# with .NET 8 server/tooling projects
- **Version Control**: Git worktrees and feature branches; integrate approved work into `UnityCode`
- **Build System**: Unity Player build pipeline, HybridCLR preparation, and .NET 8 solutions
- **Asset Pipeline**: UnityGameFramework resource pipeline, Luban configuration, and HybridCLR hot update assemblies

> **Note**: Engine-specialist agents exist for Godot, Unity, and Unreal with
> dedicated sub-specialists. Use the set matching your engine.

## Project Structure

@.claude/docs/directory-structure.md

## Engine Version Reference

@docs/engine-reference/unity/VERSION.md

## Technical Preferences

@.claude/docs/technical-preferences.md

## Coordination Rules

@.claude/docs/coordination-rules.md

## Collaboration Protocol

**User-driven collaboration, not autonomous execution.**
Every task follows: **Question -> Options -> Decision -> Draft -> Approval**

- Agents MUST ask "May I write this to [filepath]?" before using Write/Edit tools
- Agents MUST show drafts or summaries before requesting approval
- Multi-file changes require explicit approval for the full changeset
- No commits without user instruction

See `docs/COLLABORATIVE-DESIGN-PRINCIPLE.md` for full protocol and examples.

> **Brownfield project:** run `/adopt` before applying template artifact formats.
> Preserve the existing ET/UGF/HybridCLR boundaries and use `KnowledgeBase/`
> plus the repository root instructions as the source of truth.

## Coding Standards

@.claude/docs/coding-standards.md

## Context Management

@.claude/docs/context-management.md
