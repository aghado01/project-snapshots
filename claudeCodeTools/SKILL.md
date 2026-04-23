---
name: chat-review
description: >
  Produces a terse numbered summary of the current chat thread for archiving
  purposes. Trigger with `!review` or `#review` or phrases like "summarise
  this thread", "chat summary", "review this chat". Use this skill any time
  the user signals they want to capture what happened in the conversation —
  threads are not necessarily session-scoped and may span multiple sessions.
---

# Chat Review

Read the conversation from the beginning and produce a numbered, TOC-style
summary directly in your response. Do not write to a file.

## Output format

```
1. **~<start>–<end> — <Title>.** One or two sentences describing the
   conceptual work done in this segment.
2. **~<start>–<end> — <Title>.** ...
```

- `<start>` and `<end>` are approximate turn indices (count from 1, each
  human message is a new turn). Ranges are intentionally approximate —
  round to the nearest clean number rather than hunting for exact boundaries.
- **Title** is 3–6 words: noun phrase, no verb, no trailing period before
  the `.**`.
- The sentence(s) after the title describe *what happened* — decisions made,
  artefacts produced, problems resolved. Not design rationale, not
  exhaustive detail.
- Keep the whole summary skimmable. Prefer 6–12 entries covering distinct
  conceptual phases. Merge minor back-and-forth into the phase it belongs
  to rather than listing every micro-correction as its own entry.
- If the thread is short (< 5 turns), a single entry is fine.

## Calibration

The right level of detail is: someone reading the summary can reconstruct
the arc of the session and locate where specific decisions were made —
without needing to re-read the thread. It is not a design document, not a
changelog, and not a transcript.

Broad strokes > fine grain. Distinct conceptual progressions > enumerated
sub-steps.
