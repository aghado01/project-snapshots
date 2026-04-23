# Salvage Matrix

Purpose: classify vscodepilot spare parts for Copilot++ consolidation without collapsing lightweight utility tools into runtime infrastructure.

This file is the boundary map. The older inventory files remain useful as API checklists, but they are not authoritative for package boundaries or future ownership.

## Matrix

| Bucket                   | Nominal scope                                                                      | TypeScript candidates                                                                                                                                    | PowerShell candidates                                                                                                                   | Destination                                                                  | Notes                                                                                                                                                                                  |
| ------------------------ | ---------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Simple toolbelt          | Straightforward, directly callable utilities with predictable inputs and outputs   | `power-tools.ts`, `linter-ps.ts`, `structural-linter.ts`, `jso-blackbelt.ts`, `safe-shell.ts` (only while it stays thin), `copilot-toolbelt.ts` (facade) | `jso-engine.psm1`, `agent-linter-ps.psm1`                                                                                               | Lightweight toolbelt package surfaced through VS Code, CLI, and MCP adapters | Keep this low-friction, low-policy, and mostly stateless.                                                                                                                              |
| Runtime / control plane  | Background jobs, worker lifecycle, RPC host, durable state, dispatch orchestration | `parallel-tools.ts`, `job-store.ts`, `supervisor-bridge.ts`, `cybernetics-bridge.ts`                                                                     | `parallel-engine-v2.psm1`, `parallel-engine-cli.ps1`, `parallel-async-worker.ps1`, `supervisor-host.psm1`, `supervisor-host-launch.ps1` | Cybernetics runtime, likely under Automata plus Supervisor integration       | This is execution orchestration, not supervision in the policy sense.                                                                                                                  |
| Console hygiene          | Nonblocking shell dispatch, artifact reads, dump contract, console history access  | `safe-shell.ts`, parts of `copilot-toolbelt.ts`                                                                                                          | None should remain authoritative here long-term; vscodepilot only had helper-side logic                                                 | Cybernetic console system                                                    | Adapter layers may still dispatch commands, but dump path, dump reads, and console artifact semantics belong in cybernetics.                                                           |
| Oversight / policy       | Anti-spiral logic, metacognitive gating, task drift detection, discipline rules    | `copilot-primer.ts` is adjacent UX, not full policy infrastructure                                                                                       | `online-learner.psm1` is experimental and not yet a clean policy module                                                                 | Cybernetic supervision                                                       | Do not merge the old supervisor transport layer with policy supervision just because the names are similar.                                                                            |
| Context ingress / garden | Dynamic context injection, mailboxing, routing, private transport channels         | No mature standalone implementation yet in vscodepilot; future consumers will live in adapters                                                           | No mature implementation yet in vscodepilot; external salvage sources can inform design                                                 | New cybernetics context-ingress subsystem                                    | Prefer named pipes for cross-process, concurrent queues for in-process, and memory-mapped files only if payload size warrants it. The OS clipboard is at most a side-channel importer. |
| Adapter / facade         | Host-specific registration, packaging, and exposure                                | `copilot-toolbelt.ts` as API facade; host-specific registration remains outside this matrix                                                              | None                                                                                                                                    | VS Code extension, future CLI adapter, future MCP adapter                    | Adapters should stay thin and call into toolbelt or runtime layers rather than owning contracts themselves.                                                                            |

## Nominal Simple Toolbelt

These are the items that should be callable with minimal fuss and minimal hidden state.

### TypeScript

- `power-tools.ts`
- `linter-ps.ts`
- `structural-linter.ts`
- `jso-blackbelt.ts`
- `safe-shell.ts` only insofar as it remains a thin console artifact wrapper
- `copilot-toolbelt.ts` as an aggregation facade, not as a heavy subsystem

### PowerShell

- `jso-engine.psm1`
- `agent-linter-ps.psm1`

## Non-Toolbelt Items

These should not be treated as simple utilities even if they expose convenience APIs.

- `parallel-tools.ts`
- `job-store.ts`
- `supervisor-bridge.ts`
- `parallel-engine-v2.psm1`
- `parallel-engine-cli.ps1`
- `parallel-async-worker.ps1`
- `supervisor-host.psm1`
- `supervisor-host-launch.ps1`
- `online-learner.psm1`

## Classification Rules

Place an item in the simple toolbelt only if most of the following are true:

- It is directly callable without bootstrapping a long-lived process.
- It does not own durable job or session state.
- It returns deterministic, bounded results rather than coordinating a workflow.
- It does not need policy, supervision, or routing context to behave safely.
- It can be exposed from VS Code, CLI, or MCP without changing its contract.

Move an item to runtime/control plane if any of the following are true:

- It spawns or supervises background workers.
- It owns job ledgers, signal files, or process handles.
- It implements RPC or host lifecycle.
- It coordinates retries, cancellation, or persistence.

Move an item to context ingress if any of the following are true:

- It captures or routes external context into the system.
- It needs a private mailbox, queue, or transport.
- It is about dynamic context management rather than immediate user-invoked tooling.

Move an item to oversight/policy if any of the following are true:

- It detects circular behavior or task drift.
- It enforces guardrails or execution discipline.
- It evaluates patterns across actions rather than performing one tool action.

## Current Interpretation

- The old `copilot-toolbelt` split was directionally correct.
- The old `copilot-supervisor` split was still too broad.
- The refined model is:
  - simple toolbelt
  - runtime / control plane
  - oversight / policy
  - context ingress / garden
  - thin adapters

That model is a better fit for the current cybernetics consolidation path.
