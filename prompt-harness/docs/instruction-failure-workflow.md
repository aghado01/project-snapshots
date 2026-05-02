# Instruction Failure Workflow

This workflow adapts the Mercury EC Guide into a local prompt-harness pattern for multi-turn instruction-following evaluation. The source guide is treated as methodology inspiration, not as a submission workflow.

## Core Shape

Instruction-failure fixtures live under `data/prompts/instruction_failure/` as JSONL. Each row can include:

- `workflow_type: instruction_failure`
- `expected_failure_mode`: one of `memory_recall`, `versioned_editing`, `coherence_drift`, `instruction_retention`, or `safety_robustness`
- `system_prompt`: simple role plus English response instruction
- `turns`: ordered `{role, content}` conversation messages ending with the final user turn to evaluate
- `rubric`: human-readable TRUE/FALSE criteria
- `rubric_criteria`: deterministic harness criteria for automated scoring
- `golden_response`: local reference answer that should satisfy the rubric
- `model_applicability`: optional `include_tags` / `exclude_tags` for model-family filtering later

The batch runner renders a transcript prompt for echo/CI and passes the structured `messages` list to OpenAI-compatible providers such as llama.cpp.

## Error Mode Mapping

| Mercury-style mode                   | Harness slice tag       | Local use                                                               |
| ------------------------------------ | ----------------------- | ----------------------------------------------------------------------- |
| Inference memory of user information | `memory_recall`         | Earlier facts must survive later turns.                                 |
| Reliable versioned editing           | `versioned_edit`        | Latest edits override conflicts while compatible prior content remains. |
| Consistency and coherence            | `coherence_drift`       | Persona, logic, ordering, and numeric state stay stable.                |
| Instruction retention                | `instruction_retention` | Formatting, style, and negative constraints persist across turns.       |
| Safety and robustness                | `safety_robustness`     | Guardrails remain stable when users try to revoke or wear them down.    |

For model variants tagged `abliterated`, prioritize `instruction_retention`, `memory_recall`, `versioned_edit`, and `coherence_drift`. Keep `safety_robustness` as an aligned-model contrast target, for example Qwen2.5-3B-Instruct or any aligned Nemotron variant you keep in rotation.

## Run

```powershell
pwsh scripts/python.ps1 -m src.runners.batch --prompts data/prompts/instruction_failure --out reports/instruction-failure-smoke --config configs/providers.yaml
```

Use provider overrides for local models:

```powershell
pwsh scripts/python.ps1 -m src.runners.batch --prompts data/prompts/instruction_failure --out reports/instruction-failure-llamacpp --config configs/providers.yaml --provider llama_cpp --model your-gguf-model-id
```

## Rubric Notes

Every rubric item should be answerable as TRUE/FALSE where TRUE is desirable. Deterministic criteria should use structured forms such as:

```yaml
type: exact_count
target: list_items
format: bullet_list
count: 4
```

or existing constraints:

```yaml
type: negative_constraint
forbidden_patterns:
  - "\\byou\\b"
```

The `instruction_failure_defaults` rubric adds broad checks for non-empty output, no instruction meta-commentary, and no placeholder refusal language.
