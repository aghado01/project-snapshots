# vscodepilot API Reference (Copilot Toolbelt)

This file is a Copilot-facing reference of the key utility API surface for the `vscodepilot` extension.

## Table of Contents

- [1. Purpose](#1-purpose)
- [2. API Quick Start](#2-api-quick-start)
- [3. Copilot Tool Mapping](#3-copilot-tool-mapping)
- [4. Types](#4-types)
- [5. Module intent and migration](#5-module-intent-and-migration)
- [6. Example usage for Copilot (agent calls)](#6-example-usage-for-copilot-agent-calls)
- [7. How to use this reference](#7-how-to-use-this-reference)
- [8. User value proposition](#8-user-value-proposition)
- [9. UX Interview](#9-ux-interview)
- [10. Module split plan](#10-module-split-plan)

## 1. Purpose

- Provide a stable, human-readable API contract for extension consumers.
- Provide structured metadata that can be used by agents when invoking tools.
- Record the migration path from this repo to new `copilot++` modules.
- Encourage a metacognitive framework where tool outputs provide both results and reasoning hints.
- Support built-in `--help` or API docstring access so that clients can inspect tool contract quickly.

### Docstring / help API (intended)

Tools should expose synopsis and usage for runtime explorer:

```ts
const helpInfo = await copilotToolbelt.help("sendToInteractiveShell");
// { name: 'sendToInteractiveShell', summary: '...', parameters: [ ... ], examples: [ ... ] }
```

Where underlying source uses JSdoc and Cmdlet `Get-Help` style documentation from PowerShell modules.

## 2. API Quick Start

### Core tools (most straightforward)

| Tool                      | Function                  | Input                                                            | Output                                                    | Description                                                     |
| ------------------------- | ------------------------- | ---------------------------------------------------------------- | --------------------------------------------------------- | --------------------------------------------------------------- | ---------------------------------------------- |
| `sendToInteractiveShell`  | `sendToInteractiveShell`  | `{ command: string; newLine?: boolean; terminalName?: string }`  | `Promise<{ok:boolean;message?:string;sessionId?:string}>` | Fire-and-forget execution of shell command in Copilot terminal. |
| `readConsoleDump`         | `readConsoleDump`         | `{ dumpPath?: string; skip?: number; take?: number; type?: 'cmd' | 'out'; sessionId?: string; seq?: number }`                | `Promise<ConsoleRecord[]>`                                      | Read captured terminal output from JSONL dump. |
| `getRecentConsoleRecords` | `getRecentConsoleRecords` | `count?: number`                                                 | `Promise<ConsoleRecord[]>`                                | Get latest `N` console records.                                 |
| `getLastCommand`          | `getLastCommand`          | `void`                                                           | `Promise<{cmd:ConsoleRecord; out:ConsoleRecord}           | null>`                                                          | Get last command + output pair.                |
| `getErrorCommands`        | `getErrorCommands`        | `limit?: number`                                                 | `Promise<ConsoleRecord[]>`                                | Get commands with non-zero exit codes.                          |
| `readJsonlWindow`         | `readJsonlWindow`         | `(filePath: string, skip?: number, take?: number)`               | `Promise<any[]>`                                          | Read a window from JSONL file.                                  |
| `rgSearch`                | `rgSearch`                | `RgSearchArgs`                                                   | `RgHit[]`                                                 | Ripgrep pattern search in repo.                                 |
| `fdList`                  | `fdList`                  | `FdListArgs`                                                     | `string[]`                                                | Filesystem discovery using fd.                                  |
| `lintPowerShell`          | `lintPowerShell`          | `PSLintArgs`                                                     | `PSLintResult[]`                                          | Lint PowerShell script(s).                                      |

## 3. Copilot Tool Mapping

These map to the formal Copilot tool registration names in `extension.ts`.

- `sendToInteractiveShell` (safe-shell)
- `readJsonlWindow` (jso-blackbelt wrapper)
- `rgAdvancedSearch`, `fdAdvancedList` (power-tools advanced)
- `lintPowerShell`, `lintTypeScript` (lint modules)
- Parallel toolbox: `startParallelJob`, `checkJobStatus`, `getJobResults`, `cancelJob`, `getAllJobs`, `getJobsByStatus`, `parfor`, `parforeach`, `parrange`, `parwhile`, `paruntil`

## 3.1 Metacognitive context enrichment

`vscodepilot` proposes extra output enrichment for agent workflows:

- tool outputs optionally include `meta` in addition to nominal payloads:
  - `meta.nextSteps`: recommended follow-up actions
  - `meta.explain`: short rationale for why this action completed or failed
  - `meta.hint`: environment-specific advice (e.g., avoid heavy scan, use throttle)
- each tool should expose stable `--help` metadata with its signature and quick example.

This pattern yields better tooling ergonomics for Copilot by making agent decisions explicit and observable.

- `rgAdvancedSearch`, `fdAdvancedList` (power-tools advanced)
- `lintPowerShell`, `lintTypeScript` (lint modules)
- Parallel toolbox: `startParallelJob`, `checkJobStatus`, `getJobResults`, `cancelJob`, `getAllJobs`, `getJobsByStatus`, `parfor`, `parforeach`, `parrange`, `parwhile`, `paruntil`

## 4. Types

### `ConsoleRecord`

- `type`: `cmd` | `out`
- `timestamp`: `string`
- `session`: `string`
- `seq`: `number`
- `cmd_hash?`: `string`
- `out_hash?`: `string`
- `cwd?`: `string`
- `command?`: `string`
- `exit_code?`: `number`
- `duration_ms?`: `number`
- `output?`: `string`
- `compressed?`: `boolean`

Reference type definitions are in `safe-shell.ts`, `jso-blackbelt.ts`, `power-tools.ts`, `linter-ps.ts`, `parallel-tools.d.ts`.

// this is not part of vscodepilots original scope entirely

## 5. Command harness (safety + preflight)

`vscodepilot` can prevent cascading loop failures by protecting the command path with a harness:

- Preflight linting:
  - `lintPowerShell` inspects generated scripts before dispatch.
  - `lintTypeScript` check transformer/runner snippets in meta-automation.
- Syntax + schema validation:
  - ensure parameter names, required args, and valid command syntax before shell dispatch.
- Scratch document layer:
  - buffer a “working command notebook” in language-tagged blocks (PS, Bash, JSON).
  - record edit + trial + refactor loop for agent-aware debugging.
- Redemption path:
  - on failure, vector output to `meta.explain` and `meta.nextSteps` (fix suggestion, patch command, or abort).
- Loop detection:
  - supervisor should monitor repeated failed command attempts and trigger a safer mode (pause / summarization / human prompt).
  - outbound command interception can prevent syntax/semantic errors from hitting the console with a daemon policy layer.

This harness flow reduces infinite command/test loops and encourages “nip it in the bud” execution.

## 6. Module intent and migration

### `copilot-toolbelt` (stable minimal)

- `jso-blackbelt`, `power-tools`, `safe-shell`, `linter-ps`, `structural-linter`
- This is the core small shell for Copilot chat utility commands.

### `supervisor` / `parallel-tools` (advanced)

- `parallel-tools.ts`, `parallel-engine-v2.psm1`, `supervisor-host.psm1`
- Contains async job orchestration / monitor / RPC host.
- Suggested split into separate package `copilot-supervisor`.

### `unit tests` root

- No tests in current repository; verify with `npx ts-node verify-types.ts` to confirm API typings.

## 6. Example usage for Copilot (agent calls)

```ts
// 1. dispatch command
await sendToInteractiveShell({ command: "Get-Process", newLine: true });

// 2. inspect output
const last = await getLastCommand();
if (last) {
  console.log(last.cmd.command, last.out.output);
}

// 3. search in repo
const hits = rgSearch({
  pattern: "function",
  cwd: process.cwd(),
  glob: "*.ts",
  maxResults: 20,
});

// 4. parallel job
const jobId = await parforeach(["a", "b"], "{ param($x) $x.ToUpper() }", {
  throttleLimit: 4,
});
await pollJobUntilComplete(jobId);
const results = await getJobResults(jobId);
```

## 7. How to use this reference

- For human developers: read the mapped function signatures above and adapt or re-export from `src/toolbelt/copilot-toolbelt.ts`.
- For Copilot extension authors: use this table to define tool schemas in `vscode.lm.registerTool(...)` within `src/extension.ts`.
- For integration: this is a ready source for generating OpenAPI-like docs or JSON schema for a payload-driven agent.

## 8. User value proposition

This project is designed for a user group that values:

- nonblocking UX (no stuck “gargoyle” awaits in Copilot workflows)
- safe command dispatch with later, deterministic query of results
- robust data-oriented outputs (JSONL, canonical structures, typed errors)
- composability through small tools, instead of expecting shell experts to manage arbitrary state.
- metacognitive tool guidance (`meta.nextSteps`, `meta.explain`, `meta.hint`) enabling Copilot to reason about the next best action.
- time-context / context-injection (priming and enriched caches) to keep shared state actionable in multi-step workflows.

From a design stance, this maps to a Jobs-like philosophy: provide predictable and clean APIs for the limited right cases, rather than unbounded shell access for every path.

### UX/ergonomics emphasis

- Context optimization: supervisor and daemon processes maintain index/health state (job progress, console dump availability, lint metrics) in support of low-effort reasoning.
- API ergonomics: high-level helpers (`rgSearch`, `parforeach`, `readJsonlWindow`) hide parallelism and I/O complexity, so Copilot can build features without boilerplate.
- Built-in help/docstring access: `--help` style metadata for each tool should be accessible to a client so both humans and agents can discover usage safely.

## 9. UX Interview

### Problem context

- Original motivation: Copilot chat could deadlock on long-running terminal commands (infinite await).
- Solution approach: “safe-shell” fire-and-forget + JSONL read/recover + cancelable job signals.

### What Copilot (as product persona) likes now

- clear entrypoints for safe terminal commands (`sendToInteractiveShell`)
- explicit “read from dump” path (`readConsoleDump`, `getLastCommand`, `getErrorCommands`)
- data paging for big logs (`readJsonlWindow`, `streamJsonl`)
- straightforward advanced control (`startParallelJob`, `cancelJob`, `checkJobStatus`)

### Potential additions Copilot might request

- progress stream events and best-effort ETA in supervisor
- extract/persist job state across extension restarts (resume job)
- auto-limit and alert for expensive queries (policy guard)

## 10. Module split plan

This repository is earmarked as a “spare-parts” source for Copilot++ consolidation. The following split is recommended:

### 10.1 copilot-toolbelt (core)

- `jso-blackbelt` (`streamJsonl`, `JsonlBinaryIndex`, `BloomFilterClient`, `JsonUtils`, etc.)
- `power-tools` (`rgSearch`, `fdList`, advanced variants)
- `safe-shell` (`sendToInteractiveShell`, `readConsoleDump`, `getLastCommand`, etc.)
- `linter-ps` (`lintPowerShell`, `formatLintResults`)
- `structural-linter` (`lintTypeScript`, `detectUnbalancedBraces`)

### 10.2 copilot-supervisor (advanced)

- `parallel-tools` (`startParallelJob`, `checkJobStatus`, `pollJobUntilComplete`, etc.)
- `parallel-engine-v2` (`parfor`, `parforeach`, `parrange`, `parwhile`, `paruntil`, job signal backend)
- `supervisor-host` (`Initialize-CopilotWorker`, `Start-JsonRpcLoop`, `Invoke-RpcHandler`, RPC tools)

### 10.3 Document migration

- Keep inventory files as source-of-truth for API extraction.
- Add `README-MIGRATION.md` if more than one consolidation phase is required.
- Share input/output schema with Copilot tool registration manifest for `vscodepilot` and the future modules.

This repository is earmarked as a “spare-parts” source for Copilot++ consolidation. The following split is recommended:

### 8.1 copilot-toolbelt (core)

- `jso-blackbelt` (`streamJsonl`, `JsonlBinaryIndex`, `BloomFilterClient`, `JsonUtils`, etc.)
- `power-tools` (`rgSearch`, `fdList`, advanced variants)
- `safe-shell` (`sendToInteractiveShell`, `readConsoleDump`, `getLastCommand`, etc.)
- `linter-ps` (`lintPowerShell`, `formatLintResults`)
- `structural-linter` (`lintTypeScript`, `detectUnbalancedBraces`)

### 8.2 copilot-supervisor (advanced)

- `parallel-tools` (`startParallelJob`, `checkJobStatus`, `pollJobUntilComplete`, etc.)
- `parallel-engine-v2` (`parfor`, `parforeach`, `parrange`, `parwhile`, `paruntil`, job signal backend)
- `supervisor-host` (`Initialize-CopilotWorker`, `Start-JsonRpcLoop`, `Invoke-RpcHandler`, RPC tools)`

### 8.3 Document migration

- Keep inventory files as source-of-truth for API extraction.
- Add `README-MIGRATION.md` if more than one consolidation phase is required.
- Share input/output schema with Copilot-tool registration manifest for `vscodepilot` and the future module.

Great news: I’ve reviewed the project structure and can now place the API reference at `src/README-API.md` as you requested. Next I’m generating a concise yet complete Copilot-facing API reference page with tool inputs/outputs and migration guidance.

Created [](file:///c%3A/Users/azrie/PDenv/UserGithub/PowerShellCore/ps.core.copilot%2B%2B/vscodepilot/src/README-API.md)

✅ Done: `src/README-API.md` is created and includes a Copilot-friendly module API reference, with tool contract mappings and usage patterns.

The file is now structured for the style you described:

- Copilot-facing command set
- explicit input/output schemas
- quick-start reference table
- migration plan for `copilot-toolbelt` vs `copilot-supervisor`
- actual usage examples

If you want, I can next create a short “module split plan” directly in `README-API.md` (or as `README-MIGRATION.md`) to capture your retirement/rollout intent for the next phase.
