I’m grounding that split against the current code/docs so I can talk about it as a real module boundary, not just a naming preference.

Read [](file:///c%3A/Users/azrie/PDenv/UserGithub/PowerShellCore/ps.core.copilot%2B%2B/vscodepilot/ARCHITECTURE.md)

Read [](file:///c%3A/Users/azrie/PDenv/UserGithub/PowerShellCore/ps.core.copilot%2B%2B/cybernetics/ARCHITECTURE.md#1-1), lines 1 to 260

Read [](file:///c%3A/Users/azrie/PDenv/UserGithub/PowerShellCore/ps.core.copilot%2B%2B/vscodepilot/src/typescript/power-tools.ts#1-1), lines 1 to 260

Read [](file:///c%3A/Users/azrie/PDenv/UserGithub/PowerShellCore/ps.core.copilot%2B%2B/cybernetics/CopilotSupervision.psm1#1-1), lines 1 to 260

Read [](file:///c%3A/Users/azrie/PDenv/UserGithub/PowerShellCore/ps.core.copilot%2B%2B/vscodepilot/src/typescript/supervisor-bridge.ts#1-1), lines 1 to 260

I’ve got enough context. The key correction is that the old vscodepilot supervisor should not merge into cybernetic supervision just because the names rhyme; one is control-plane execution, the other is oversight/policy.

Inserted into memory file [](file:///c%3A/Users/azrie/AppData/Roaming/Code/User/workspaceStorage/74ec3837a73de60c51ed9518d13942ec/GitHub.copilot-chat/memory-tool/memories/repo/vscodepilot-cybernetics-merge.md)

That split is the right one, and the current code already points in that direction.

**Boundary**

- power-tools.ts is exactly the kind of thing that should stay in a simple utility-belt layer. It is fast, mostly stateless, and directly callable.
- supervisor-bridge.ts is not a utility. It is a control-plane transport for long-lived execution.
- CopilotSupervision.psm1 is also not the same thing as the old vscodepilot supervisor. It is oversight, pattern detection, and anti-spiral policy.
- So I would not merge supervisor into supervision as one concept. I would split them harder:
- Control plane and execution orchestration go toward cybernetics runtime, probably alongside CyberneticAutomata.psm1 and possibly CyberneticSupervisor.psm1.
- Oversight and discipline stay in CopilotSupervision.psm1.
- Console hygiene belongs with CyberneticConsole.psm1, not with the extension shell.
- Straightforward tools stay separate and thin so they can be exposed from VS Code, CLI, and MCP without dragging the whole runtime in.

The clean product shape is starting to look like this:

- Cybernetics core: runtime, console, execution, memory, supervision, context infrastructure.
- Utility belt: search, lint, token, hash, file helpers, simple JSONL readers, other low-friction tools.
- Adapters: VS Code extension, CLI, and MCP all sit on top of the same two lower layers.
- Advanced context infrastructure: clipboard ingress, dynamic context injection, mailboxing, routing, suppression rules.

That also means ARCHITECTURE.md is probably heading toward an outdated dependency story. Right now it still frames things as vendored extension dependencies versus external host contracts. The direction you just described is more like a shared platform with multiple adapters, which aligns more with ARCHITECTURE.md.

**Clipboard**
I agree with your framing: the clipboard system should not live in the simple toolbelt. It is advanced infrastructure.

I would treat it as a context ingress service, not as a direct tool:

- it observes or captures clipboard events
- classifies and normalizes them
- writes artifact records into a mailbox or context stream
- lets VS Code, CLI, MCP, or other agents consume that state deliberately

That keeps it in the same family as console hygiene and context management, not in the same family as power-tools or linters. The attached PSOneTools clipboard code is useful mainly as raw ingress mechanics, while simpler PSOneTools pieces like tokenization, hashing, or duplicate-file helpers are much better utility-belt candidates.

The naming matters here too. If you keep calling all of this supervisor, supervision, console, clipboard, and toolbelt under one umbrella, the boundaries will blur again. I would explicitly use three buckets:

- Runtime services
- Utility tools
- Context ingress and management

The next useful excavation passes would be:

1. Sweep old vscodepilot and PSOneTools code for utility-belt candidates only.
2. Separately map the control-plane pieces that belong in cybernetics runtime.
3. Sketch the context-ingress layer as its own subsystem, with clipboard as one source rather than the whole feature.

The honest answer is: **the OS clipboard is a single shared resource and Windows provides no native API for multiple isolated clipboards**. There's no true siloing at the clipboard level — any process can read or write it at any time.

However the hygiene concern is solvable from the other direction — don't use the OS clipboard as the transport at all. The clipboard listener pattern from PSOne is interesting precisely because it _monitors_ the clipboard as a side channel, but for a controlled internal workspace you want a **purpose-built channel** that the OS can't accidentally corrupt. The options in roughly ascending complexity: [ppl-ai-file-upload.s3.amazonaws](https://ppl-ai-file-upload.s3.amazonaws.com/web/direct-files/attachments/34894665/c3c5c3f1-68e0-4314-9ba7-b6483d91bcf0/Start-PSOneClipboardListener.ps1)

## Named Pipe

```powershell
$pipe = [System.IO.Pipes.NamedPipeServerStream]::new(
    'copilot-garden',
    [System.IO.Pipes.PipeDirection]::InOut
)
```

A named pipe gives you a private bidirectional channel with controlled entry — only code that knows the pipe name and has appropriate permissions can connect. No OS interaction surface, no accidental corruption, reads block until a writer connects. This is essentially what you want: a clipboard-equivalent that's scoped to your session.

## `ConcurrentQueue` in a shared runspace variable

If everything is in-process (same PS session), a `[System.Collections.Concurrent.ConcurrentQueue[string]]` held in a well-known `$script:` or module-scoped variable is the simplest possible private channel — zero OS surface, typed, thread-safe, and invisible to anything outside the session.

## Memory-mapped file

If you need cross-process within the same machine with controlled access, a named `MemoryMappedFile` gives you a private shared-memory region — faster than pipes for large payloads, same isolation guarantee.

## The Design Principle

The PSOne clipboard listener is the right inspiration but the wrong transport for hygiene-conscious work — replace the OS clipboard as the medium with a named pipe or in-process queue, keep the same `[hashtable]::Synchronized` + background runspace pattern for the listener side. You get all of PSOne's lifecycle management with none of the OS contamination risk. The "controlled entry point" becomes the pipe connection or queue reference rather than whatever the user last copied. [ppl-ai-file-upload.s3.amazonaws](https://ppl-ai-file-upload.s3.amazonaws.com/web/direct-files/attachments/34894665/c3c5c3f1-68e0-4314-9ba7-b6483d91bcf0/Start-PSOneClipboardListener.ps1)

I’m recording this clarification because it changes the architectural target: the clipboard is a heuristic ingress source at most, not the transport.

Inserted into memory file [](file:///c%3A/Users/azrie/AppData/Roaming/Code/User/workspaceStorage/74ec3837a73de60c51ed9518d13942ec/GitHub.copilot-chat/memory-tool/memories/repo/vscodepilot-cybernetics-merge.md)

This is the right reframing. The thing in majestic-garden.md is not really a clipboard feature, it is a private context ingress bus.

That fits your product split much better:

- simple toolbelt stays as direct-call utilities like search, lint, token, hash, file helpers
- cybernetics owns the heavier runtime and context machinery
- the “garden” or ingress layer sits with advanced infrastructure, alongside console hygiene, supervision, and context management
- the OS clipboard becomes, at most, an optional importer into that system, not the system itself

The key shift is from “watch the shared clipboard” to “own a session-scoped channel.” That is the principled version of what the old PSOne listener was groping toward. The listener pattern is still useful, but only for lifecycle mechanics: STA runspace, synchronized state, background loop, clean shutdown. The transport should be private.

If you want cross-process on Windows, named pipe is the right first default. If you want same-process only, a `ConcurrentQueue` is simpler and cleaner. I would not start with memory-mapped files unless payload size or throughput actually becomes the bottleneck.

Architecturally, I’d treat it like this:

- `power-tools` and similar remain thin callable tools
- cybernetics grows a context-ingress subsystem
- VS Code, CLI, and MCP become clients of that ingress subsystem
- clipboard, console, files, maybe even issue comments later are just sources feeding the same normalized channel

The most important design rule is: producers write envelopes into the garden, consumers read from it. No source should directly inject into Copilot state. That keeps routing, filtering, logging, retention, and supervision centralized.

If you want, the next useful step is one of these:

1. Define the canonical garden message envelope.
2. Decide whether the first transport is named pipe or in-process queue.
3. Map where this subsystem lives in cybernetics relative to console, supervision, and automata.
