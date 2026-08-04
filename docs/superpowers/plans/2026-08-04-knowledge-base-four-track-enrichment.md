# Knowledge Base Four-Track Enrichment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the repository knowledge base with source-backed AI/CCGS collaboration, Unity runtime, ET server runtime, and comprehensive scripting-standard guidance.

**Architecture:** Add four focused knowledge pages mapped to nine catalog modules. Assign each page to an isolated implementation session, then run exactly one new read-only review session for that module. Integrate confirmed findings in the main worktree, update the existing editor-tools page and validator where review exposes shared gaps, then refresh navigation and source fingerprints without absorbing unrelated working-tree changes.

**Tech Stack:** Markdown, JSON, PowerShell, codedb-mcp, Git

---

### Task 1: Record The Baseline And Evidence Boundaries

**Files:**
- Read: `AGENTS.md`
- Read: `KnowledgeBase/_template.md`
- Read: `KnowledgeBase/LOOP.md`
- Read: `KnowledgeBase/catalog.json`
- Read: `KnowledgeBase/source-fingerprints.json`
- Test: `KnowledgeBase/Test-KnowledgeBase.ps1`

- [x] **Step 1: Capture the repository baseline**

Run:

```powershell
git status --short --branch
git diff --name-only
git ls-files --others --exclude-standard
```

Expected: the branch is `codex/UnityCode`; pre-existing Unity, Luban, Week Eight, WorkBuddy, `obj.wb`, test, output, and concept files remain outside this plan's write set.

- [x] **Step 2: Verify the knowledge page contract**

Run:

```powershell
Get-Content -Raw -Encoding UTF8 KnowledgeBase/_template.md
Get-Content -Raw -Encoding UTF8 KnowledgeBase/LOOP.md
```

Expected: every new page must contain the eleven required headings from `模块定位` through `关联知识`, including `源码证据`, and static completion must remain distinct from runtime acceptance.

- [x] **Step 3: Run the pre-change validator**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File KnowledgeBase/Test-KnowledgeBase.ps1
```

Expected: either the baseline passes, or fingerprint failures identify only pre-existing external changes. Record failures before editing and do not refresh fingerprints in the dirty worktree.

### Task 2: Document AI Development And CCGS Collaboration

**Files:**
- Create: `KnowledgeBase/28-AI开发工作流与CCGS协作.md`
- Read: `AGENTS.md`
- Read: `KnowledgeBase/LOOP.md`
- Read: `.agents/plugins/marketplace.json`
- Read: `plugins/ccgs-codex/.codex-plugin/plugin.json`
- Read: `plugins/ccgs-codex/skills/ccgs/SKILL.md`
- Read: `plugins/ccgs-codex/skills/ccgs/references/compatibility.md`
- Read: `plugins/ccgs-codex/skills/ccgs/references/workflow-index.md`

- [x] **Step 1: Extract executable AI rules**

Inspect the repository instruction order, knowledge-loop gates, plugin manifest, `$ccgs` router, compatibility mappings, workflow index, role loading, Unity Agent Bridge preconditions, and evidence requirements. Separate repository-enforced behavior from optional CCGS recommendations.

- [x] **Step 2: Write the AI collaboration page**

Create a page with catalog mappings `AI-01` and `AI-02`. Cover instruction precedence, task intake, tool routing, 73 workflows, 49 on-demand roles, Codex substitutions for Claude-only semantics, multi-agent write boundaries, Unity bridge prerequisites, evidence capture, review, and commit gates.

- [x] **Step 3: Check every claim against exact source locations**

Run:

```powershell
Select-String -Path KnowledgeBase/28-AI开发工作流与CCGS协作.md -Pattern 'AGENTS.md|LOOP.md|plugins/ccgs-codex|marketplace.json'
```

Expected: the page contains repository-relative evidence for instructions, the loop, plugin routing, compatibility, and workflow inventory.

### Task 3: Document The Unity Runtime Chain

**Files:**
- Create: `KnowledgeBase/29-Unity运行链路.md`
- Read through codedb-mcp: `Unity/Assets/Scripts/Game/Procedure/ProcedureLaunch.cs`
- Read through codedb-mcp: exact startup, loader, `HotEntry`, table, resource, UI, Entity, and scene files returned by graph evidence

- [x] **Step 1: Discover startup and cross-module dependencies**

Use `codedb_graph_query` to trace from the exact `ProcedureLaunch` entry through mode selection, loader initialization, code loading, and the GameHot entry. Continue cross-file investigation only through graph-returned files and symbols.

- [x] **Step 2: Verify readiness orchestration**

Use graph queries plus exact outlines or symbol bodies to establish configuration loading, resource readiness, procedure transitions, UI opening, Entity display, and scene changes. Preserve conditional-compilation and mode-selection branches rather than collapsing them into one unconditional path.

- [x] **Step 3: Write the Unity runtime page**

Create a page with catalog mappings `UNITY-17` and `UNITY-18`. Describe the startup sequence, readiness gates, state ownership, extension procedure, common failure points, static verification, and the separate Unity Editor runtime acceptance steps.

- [x] **Step 4: Cross-link existing module pages**

Link the runtime page to the architecture, GameHot, UI, Entity, AssetSet, scene, Luban, and hot-update pages instead of repeating their API inventories.

### Task 4: Document The ET Server Runtime Chain

**Files:**
- Create: `KnowledgeBase/30-ET服务端运行链路.md`
- Read through codedb-mcp: `DotNet/App/Program.cs`
- Read through codedb-mcp: `DotNet/Loader/Init.cs`
- Read through codedb-mcp: exact `CodeLoader`, Scene/Fiber, network, message-dispatch, Actor/Location, and Hotfix files returned by graph evidence

- [x] **Step 1: Trace process initialization**

Use graph evidence to follow process entry, options/configuration, logging, code loading, assembly registration, Scene/Fiber creation, and the update loop. Mark compile-time and runtime alternatives explicitly.

- [x] **Step 2: Trace request handling**

Use `CALLS`, `DISPATCHES_TO`, `REFERENCES`, and exact source reads to connect network input to message dispatch, Actor/Location routing, generated protocol contracts, and Hotfix handlers. Do not describe a possible dispatch target as the unique runtime target without registration or configuration evidence.

- [x] **Step 3: Write the ET server runtime page**

Create a page with catalog mappings `SERVER-04` and `SERVER-05`. Include startup and request sequence diagrams, state/lifecycle ownership, extension steps for a new service and handler, failure modes, static checks, and runtime smoke-test commands that remain unexecuted unless actually run.

- [x] **Step 4: Cross-link existing server pages**

Link to ET integration, Proto, .NET server, network/lockstep, and admin/scaling pages for detailed APIs.

### Task 5: Document Comprehensive Scripting Standards

**Files:**
- Create: `KnowledgeBase/31-脚本编写规范.md`
- Modify: `KnowledgeBase/19-编辑器工具集.md`
- Modify: `KnowledgeBase/Test-KnowledgeBase.ps1`
- Read: `AGENTS.md`
- Read: `.claude/rules/*.md`
- Read: `plugins/ccgs-codex/skills/ccgs/references/standards/*`
- Read: exact analyzer and source-generator files under `Share/Analyzer/` and `Share/SourceGenerator/`
- Read: representative tracked C# files for GameHot, ET, Editor, UI, Entity, tests, build, Luban, and Proto
- Read: representative tracked PowerShell and BAT files under `Tools/Shell/`

- [x] **Step 1: Classify rule authority**

Build four evidence levels inside the page: compiler/analyzer enforced, repository implementation pattern, written repository convention, and advisory team convention. Resolve conflicts in favor of actual assembly boundaries and source behavior.

- [x] **Step 2: Cover runtime and ET C# rules**

Document directory and assembly placement, naming, hot-update boundaries, Procedure/UI/Entity lifecycle, async cancellation and forgotten tasks, event subscription symmetry, logging and exceptions, ET Entity/Component/System rules, Model/Hotfix separation, shared client/server code, generated messages, and code-generation boundaries.

- [x] **Step 3: Cover editor, tests, and automation rules**

Document Editor/Runtime isolation, generated-directory immutability, analyzer/source-generator expectations, test naming and isolation, PowerShell/BAT path handling, UTF-8 encoding, quoting, exit-code propagation, external-command failure checks, secrets, Luban/Proto/build ordering, release verification, and narrowly scoped commits.

- [x] **Step 4: Add checklists and counterexamples**

Provide concise pre-code, implementation, review, and pre-commit checklists. For risky rules, include a repository-consistent example and an explicit forbidden pattern without inventing APIs.

- [x] **Step 5: Close shared tooling gaps found during review**

Update the editor-tools inventory when current tool source is missing, and make source fingerprints use Git clean-filter object IDs so CRLF conversion does not create false source drift. Verify the validator still detects real unstaged content changes and fails clearly when Git is unavailable.

### Task 6: Register Modules And Navigation

**Files:**
- Modify: `KnowledgeBase/catalog.json`
- Modify: `KnowledgeBase/README.md`

- [x] **Step 1: Add nine catalog modules**

Add `AI-01`, `AI-02`, `UNITY-17`, `UNITY-18`, `SERVER-04`, `SERVER-05`, `CODE-01`, `CODE-02`, and `CODE-03`. Give each module independent stable sources, keywords, area, document path, and `verified` status only after source-by-source review.

- [x] **Step 2: Validate JSON structure**

Run:

```powershell
Get-Content -Raw -Encoding UTF8 KnowledgeBase/catalog.json | ConvertFrom-Json | Out-Null
```

Expected: command exits 0 without a JSON parse exception.

- [x] **Step 3: Update the knowledge index**

Change the README summary from 45 to 54 modules and from pages `01-27` to `01-31`. Add reading paths for AI collaboration, Unity runtime, ET server runtime, and scripting standards while retaining the static/runtime acceptance distinction.

### Task 7: Refresh Fingerprints In An Isolated Clean Worktree

**Files:**
- Modify: `KnowledgeBase/source-fingerprints.json`
- Test: `KnowledgeBase/Test-KnowledgeBase.ps1`

- [x] **Step 1: Review the exact knowledge-base diff**

Run:

```powershell
git diff -- docs/superpowers/plans/2026-08-04-knowledge-base-four-track-enrichment.md KnowledgeBase/19-编辑器工具集.md KnowledgeBase/28-AI开发工作流与CCGS协作.md KnowledgeBase/29-Unity运行链路.md KnowledgeBase/30-ET服务端运行链路.md KnowledgeBase/31-脚本编写规范.md KnowledgeBase/Test-KnowledgeBase.ps1 KnowledgeBase/catalog.json KnowledgeBase/README.md
```

Expected: only this plan, the four new pages, editor-tools correction, validator, catalog, and navigation changes appear.

- [x] **Step 2: Reproduce the knowledge changes in a clean worktree**

Create a temporary detached worktree from the current branch, copy only the files in this plan's write set into it, and verify `git status --short` there contains no external Unity, Luban, Week Eight, WorkBuddy, `obj.wb`, test, output, or concept changes.

- [x] **Step 3: Refresh fingerprints in the clean worktree**

Run in the temporary worktree:

```powershell
powershell -ExecutionPolicy Bypass -File KnowledgeBase/Test-KnowledgeBase.ps1 -RefreshSourceFingerprints
```

Expected: all 54 catalog modules receive fingerprints based only on the committed source paths declared by the catalog; copied knowledge pages and unrelated working-tree files do not enter source hashes.

- [x] **Step 4: Copy back only the fingerprint file**

Copy `KnowledgeBase/source-fingerprints.json` from the clean worktree to the main worktree, then remove the temporary worktree using `git worktree remove` after verifying its absolute path.

### Task 8: Validate, Review, And Commit

**Files:**
- Test: `KnowledgeBase/Test-KnowledgeBase.ps1`
- Review: all files changed by this plan

- [x] **Step 1: Run both static validation gates in the clean worktree**

Run:

```powershell
powershell -ExecutionPolicy Bypass -File KnowledgeBase/Test-KnowledgeBase.ps1
powershell -ExecutionPolicy Bypass -File KnowledgeBase/Test-KnowledgeBase.ps1 -RequireStaticComplete
```

Expected: both commands exit 0, report 31 knowledge pages and 54 verified catalog modules, and do not mark runtime acceptance checks as passed.

- [x] **Step 2: Run one independent review session per module session**

After each of the four implementation sessions completes, start one new read-only review session for that module. Confirm every key call-chain claim has an exact repository-relative source citation, every page has all required headings, all internal links resolve, and no runtime action is described as executed without evidence. Do not run default triple review; create a further discussion agent only for a concrete disputed finding or when the user explicitly requests more review.

- [x] **Step 3: Stage only the approved write set**

Run:

```powershell
git add -- docs/superpowers/plans/2026-08-04-knowledge-base-four-track-enrichment.md KnowledgeBase/19-编辑器工具集.md KnowledgeBase/28-AI开发工作流与CCGS协作.md KnowledgeBase/29-Unity运行链路.md KnowledgeBase/30-ET服务端运行链路.md KnowledgeBase/31-脚本编写规范.md KnowledgeBase/Test-KnowledgeBase.ps1 KnowledgeBase/catalog.json KnowledgeBase/README.md KnowledgeBase/source-fingerprints.json
git diff --cached --name-only
```

Expected: the staged list contains exactly the ten knowledge/plan files named above; unrelated working-tree changes remain unstaged.

- [x] **Step 4: Commit the knowledge enrichment**

Run:

```powershell
git commit -m "docs: expand AI and runtime knowledge base"
```

Expected: commit succeeds on `codex/UnityCode` and leaves all pre-existing external changes untouched.
