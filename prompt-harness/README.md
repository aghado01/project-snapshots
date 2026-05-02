# Prompt Harness Quality Lab

> Evaluation-first development for conversational AI systems

Built for rapid, slice-aware quality regression detection. Aligns with AI Data Specialist workflows (truthfulness, citations, instruction adherence) inspired by holistic evaluation approaches.

## Highlights

- **Provider abstraction** with retry/backoff, typed request/response dataclasses, a registry pattern, and a deterministic echo provider for offline testing — solid engineering
- **Citation F1 scoring** (precision/recall/f1 on `[N]`-style citations) — a real metric with real semantics
- **Rubric engine** supporting regex and keyword-presence criteria with weighted scoring — pluggable and YAML-driven
- **Slice labeling** — auto-tags prompts by length, vagueness, citation need, constraint density; supports explicit override tags
- **Batch runner** — full pipeline: load prompts → generate → score citations → evaluate rubric → label slices → aggregate stats → write traces/CSV/report
- **Gate evaluator** — compares candidate vs baseline on named metrics with absolute min/max AND relative drop/increase gates — this is the CI regression story
- **Jinja2 report engine** — properly abstracted with fallback rendering, bytecode cache, choice loader chain
- **Context probes** — Python-native tokenizer/context-window probe generation with template-rendered payload mode for intentionally malformed JSON surfaces

## Phase 1 Quick Start

Create a baseline run with the deterministic echo provider:

```
pwsh scripts/run_baseline.ps1
```

Artifacts written to `reports/baseline-<timestamp>/`:

- `traces.jsonl` raw per-prompt records
- `summary.csv` flat row summary (latency + tokens)
- `run_stats.json` aggregate statistics (p50/p95 latency, avg tokens)

Manual invocation with explicit overrides:

```
pwsh scripts/python.ps1 -m src.runners.batch --config configs/providers.yaml --prompts data/prompts --out reports/test-run --provider echo --model echo-small --seed 42
```

Next phases add metrics, slices, rubric scores, and regression gates. Phase 1 ensured deterministic, reproducible artifact generation end-to-end.

## Why This Matters for Conversational AI

- **Truthfulness & Citations**: Inline citation extraction + F1 scoring
- **Helpfulness / Instruction Following**: Rubric-driven rule evaluation (extensible to LLM-as-judge)
- **Slice Analysis**: Length, ambiguity, constraint, and citation-needs stratification to target weaknesses
- **Regression Gates**: CI-enforced thresholds prevent silent quality drift
- **Production Habits**: Deterministic seeds, trace persistence, markdown reporting

## Reporting & Gates (Phase 2 additions)

Each run now also writes `report.md` summarizing global stats and slice averages, plus:

- `slices.json` per-slice aggregates (latency, citation F1, rubric score)
- `gates_report.json` (after compare) with pass/fail metadata
- Baseline alias: latest baseline directory is exposed at `reports/baseline-latest` via junction

Run a candidate and compare against the baseline alias:

```
pwsh scripts/run_baseline.ps1   # refresh baseline & alias
pwsh scripts/python.ps1 -m src.runners.batch --out reports/candidate-test --prompts data/prompts --config configs/providers.yaml
pwsh scripts/compare_gates.ps1 -Candidate reports/candidate-test
```

Gate thresholds are defined in `configs/gates.yaml`. Adjust initial relaxed thresholds upward as real providers are integrated.

## Quick Start (30 seconds)

```
pwsh scripts/start_background_run.ps1 -Name ensure-venv -ScriptPath scripts/ensure_venv.ps1
pwsh scripts/start_background_run.ps1 -Name baseline -ScriptPath scripts/run_baseline.ps1
pwsh scripts/get_background_run.ps1
pwsh scripts/start_background_run.ps1 -Name candidate -ScriptPath scripts/python.ps1 -m src.runners.batch --out reports/candidate --prompts data/prompts
pwsh scripts/start_background_run.ps1 -Name gates -ScriptPath scripts/compare_gates.ps1 -Candidate reports/candidate
```

Local automation defaults to background runs that write `status.json`, `stdout.log`, and `stderr.log` under `reports/runs/<run-id>/`. CI still calls the foreground scripts directly for exit-code propagation.

Generate context probe fixtures:

```
pwsh scripts/start_background_run.ps1 -Name probe-generation -ScriptPath scripts/generate_probe_payloads.ps1 -EnsureVenv
pwsh scripts/start_background_run.ps1 -Name context-probes -ScriptPath scripts/python.ps1 -m src.runners.batch --out reports/context-probes --prompts data/prompts/context_probes --config configs/providers.yaml
```

Run Mercury-inspired instruction-failure fixtures:

```
pwsh scripts/start_background_run.ps1 -Name instruction-failure -ScriptPath scripts/python.ps1 -m src.runners.batch --out reports/instruction-failure-smoke --prompts data/prompts/instruction_failure --config configs/providers.yaml
```

## Local Workflow Reference

See [docs/workflow-reference.md](docs/workflow-reference.md) for the explicit portable Python wrapper, local venv bootstrap, context probe workflow, llama.cpp stack notes, and GGUF model inventory workflow.

See [docs/instruction-failure-workflow.md](docs/instruction-failure-workflow.md) for the local multi-turn instruction-failure workflow inspired by the Mercury EC guide.

For contributor and coding-assistant orientation, see [AGENTS.md](AGENTS.md).

## Demo Flow (5 minutes)

1. Baseline run & artifacts
2. Candidate run (simulate new model) -> gates compare
3. Open `report.md` to show slice metrics & rubric scores
4. Show failing/passing gates logic (if thresholds tightened)
5. Highlight CI workflow `.github/workflows/ci.yml`

## Future Enhancements

- Delta-vs-baseline gates (relative change tolerance)
- Cost tracking & per-slice cost efficiency
- LLM-as-judge secondary rubric layer (pairwise preference, factuality)
- HTML dashboard export

---

Portfolio talking points: slice-aware evaluation, regression gates, reproducibility, CI enforcement, extensible rubric & metric architecture.
