# ps.core.vscodepilot-ext — Session Digest

> Synthesized from `i'm still getting a suggestion...` (lines 1050–1550, 2100–2250)

---

## Overview

ps.core.vscodepilot-ext is the **thin adapter layer** at the boundary between VS Code's volatile API surface and the stable PowerShellCore internals. The session established that this module should contain **zero domain logic** — it exists purely to translate between the bootstrap/supervision/memory substrate and whatever API surface VS Code currently exposes. The Chat Participant API is its primary operational lever.

---

## 1. Full Component Audit

The session performed a comprehensive audit of every file in the extension:

### ✅ Safe — Keep and Maintain
| Component | Role | Status |
|:----------|:-----|:-------|
| `safe-shell` | Fire-and-forget shell execution with JSONL capture | Stable, battle-tested |
| `power-tools` | CLI wrappers (rg, fd) — platform-agnostic | Stable, useful |
| `copilot-toolbelt` | JSONL retrieval (readJsonlWindow, findEventsByHash, getRecentSupervision) | Stable core |
| `supervisor-bridge` | JSON-RPC IPC to CyberneticSupervisor process | Clean contract |
| `parallel-jobs` | Parallel execution orchestration | Stable |

### ❌ Broken or Fragile
| Component | Issue | Action |
|:----------|:------|:-------|
| `ADJUTANT_PROTOCOL` | Fragile prompt injection pattern, relies on model compliance | Redesign or remove |
| `linter.ts` | Missing / incomplete | Either implement properly or don't ship |

### ⚠️ Wrong Delivery Mechanism
| Component | Issue | Correct Approach |
|:----------|:------|:-----------------|
| `copilot-primer.ts` | Currently injects priming via wrong channel | Should use Chat Participant system message |
| Engine version check | Stale, hardcoded | Should be dynamic or removed |

---

## 2. Five Context Buffer Access Points

The session identified exactly five places where content can be injected into a Copilot context window:

1. **`copilot-instructions.md`** — Static, disk-based, loaded once per session. Most stable but least dynamic.
2. **Tool schema `description` fields** — In `package.json`. Always present in every payload. Stable channel for behavioral hints.
3. **Chat Participant system message** — Per-turn, fully dynamic, set in the `handleRequest` callback. **This is the primary lever.**
4. **Tool return values** — Mid-loop injection between agentic passes. Ideal for supervision signals and `_meta` hints.
5. **`#`-mention references** — Per-turn, user-initiated or programmatic. File/URL content injection.

Plus two research-grade surfaces:
- **Synthetic tool results** — Uninvited injection, higher trust signal than system message, but fragile and undocumented
- **HTTPS proxy rewrite** — Full payload access, nuclear option, research-only

---

## 3. Seven Injection Surfaces — Ranked Stability → Power

| Rank | Surface | Mechanism | When to Use |
|:-----|:--------|:----------|:------------|
| 1 | `copilot-instructions.md` | Disk write | Session-scoped priming, stable beliefs |
| 2 | Tool descriptions | `package.json` schema | Always-on behavioral nudges |
| 3 | Participant system message | Per-turn callback | Dynamic priming, active state injection |
| 4 | Tool return values | Per-pass mid-loop | Supervision, mid-task correction |
| 5 | `#`-mention references | Per-turn content | User-directed or programmatic context |
| 6 | Synthetic tool results | Uninvited injection | Research only — fragile |
| 7 | Proxy rewrite | Full payload mutation | Nuclear — research only |

---

## 4. Tool Description Engineering

A key insight: tool `description` fields in `package.json` are a stable, always-available injection surface. They appear in every payload. Design them intentionally:

```json
{
  "name": "readJsonlWindow",
  "description": "Retrieve recent supervision events from the JSONL stream. Returns structured supervision data. IMPORTANT: When results contain contradiction signals, re-evaluate your current approach before proceeding."
}
```

The description is *behavioral documentation that the model actually reads*. It's the one place you can encode supervision heuristics that survive context window compaction.

---

## 5. Return Value Steering with `_meta` Hints

Tool return values can include structured `_meta` fields that steer model behavior mid-loop:

```json
{
  "result": { ... },
  "_meta": {
    "confidence": 0.7,
    "suggestion": "Consider running tests before proceeding",
    "context_hint": "This module has known fragility around async initialization"
  }
}
```

The model treats tool returns as high-trust information (higher than system messages in practice). This makes tool returns an excellent channel for mid-loop supervision signals.

---

## 6. The Proxy Supervisor as Anterior Cingulate

The proxy supervisor sits between the assembled payload and the model endpoint. Its role is analogous to the anterior cingulate cortex — conflict detection and error signaling:

- **Passive mode**: Captures full payload, logs token budget breakdown, records sealed system prompt hash
- **Active mode**: Evaluates assembled context for conflicts, redundancy, budget violations
- **Intervention mode**: Can rewrite payload (research only) or inject additional tool results

### Token Budget Breakdown
The proxy decomposes every payload into:
- Sealed system prompt (not yours, ~constant)
- Injected instructions (copilot-instructions.md + participant system message)
- Tool schemas (all registered tools)
- Conversation history (growing)
- Available context budget (what's left)

This visibility is essential — without it, you're blind to how much of "your" context actually reaches the model.

---

## 7. Copilot Performance Interventions

The session diagnosed five failure modes and ranked five fixes:

### Five Failure Modes
1. **Context overflow** — too much injected, model loses focus
2. **Stale priming** — instructions reference deleted files or obsolete patterns
3. **Tool schema bloat** — too many tools, descriptions eat context budget
4. **Conflicting signals** — system message says X, tool return says Y
5. **Instruction drift** — copilot-instructions.md evolves without validation

### Five Ranked Fixes
1. **Slim tool descriptions** — audit and compress every tool description
2. **Dynamic priming** — participant system message based on active state, not static
3. **Budget monitoring** — proxy sensor + alerts when context utilization exceeds threshold
4. **Signal chain validation** — ensure system message, tool returns, and instructions align
5. **Priming write-back** — automated update of copilot-instructions.md at session boundaries

---

## 8. Architectural Principle: Pure Thin Adapter

The session established a hard architectural rule:

> The ext should contain **zero domain logic**. No clustering. No prediction. No memory management. No priming assembly. Just translation.

```
[ bootstrap substrate ]      ← all logic lives here
       ↓  clean protocol (JSONL, JSON-RPC, MCP)
[ vscodepilot-ext ]          ← pure adapter: translate protocol ↔ VS Code API
       ↓  VS Code Extension API
[ Copilot / VS Code ]        ← volatile, not yours
```

### Why This Matters
- VS Code's API surface is **volatile** — it changes with every release
- Your domain logic is **stable** — it evolves on your schedule
- Mixing them creates gargoylism (see bootstrap DIGEST.md)
- The adapter pattern means VS Code API breaks require fixing **one thin layer**, not rewriting logic

### What Belongs in Ext
- Chat Participant registration and `handleRequest` callback
- Tool registration (`vscode.lm.registerTool`)
- Extension activation/deactivation lifecycle
- IPC connection to bootstrap subprocess
- Translation of VS Code events to JSONL events

### What Does NOT Belong in Ext
- Priming content assembly (→ bootstrap)
- Supervision logic (→ bootstrap/CyberneticSupervisor)
- Memory retrieval or promotion (→ bootstrap/memory layer)
- Clustering or prediction (→ pwshspc)
- Snapshot generation (→ reposnapshot)

---

## 9. Chat Participant API — Primary Lever

The Chat Participant API registration is the single most important code in the ext:

```typescript
vscode.chat.createChatParticipant('powershellcore', async (request, context, stream, token) => {
    // 1. Query bootstrap for active priming state
    const priming = await bridge.getPriming(request.command);
    
    // 2. Set system message (PRIMARY INJECTION SURFACE)
    context.systemMessage = priming.systemMessage;
    
    // 3. Forward to model
    // 4. Capture response in JSONL
    // 5. Return with _meta hints if supervision signals exist
});
```

Everything flows through this callback. The ext's entire value is in making this callback correctly translate between bootstrap's rich state and VS Code's API surface.

---

## 10. Dependency Inversion at the Volatility Boundary

```
Stable                                    Volatile
┌─────────────────┐                    ┌──────────────────┐
│  bootstrap       │                    │  VS Code API     │
│  supervision     │  ← interface →    │  Copilot Chat    │
│  memory          │                    │  LM Extension    │
│  pwshspc         │                    │  Terminal API    │
└─────────────────┘                    └──────────────────┘
         ↑                                      ↑
         └──── both depend on the interface ────┘
               (JSONL schema, MCP, JSON-RPC)
```

Neither side depends on the other directly. Both depend on the **protocol**. This is the dependency inversion principle applied at an architecture boundary.

---

## Open Items

- [ ] Audit all tool descriptions for compression — remove redundant words, encode behavioral hints
- [ ] Implement Chat Participant `handleRequest` as pure bootstrap delegation
- [ ] Remove or redesign ADJUTANT_PROTOCOL (fragile prompt injection)
- [ ] Fix copilot-primer.ts delivery mechanism (use participant system message, not current approach)
- [ ] Implement `_meta` hint schema in tool return values
- [ ] Plan absorption of safe-shell, power-tools, copilot-toolbelt into bootstrap
- [ ] Validate ext contains zero domain logic — audit for leakage
- [ ] Implement dynamic engine version detection or remove stale check
