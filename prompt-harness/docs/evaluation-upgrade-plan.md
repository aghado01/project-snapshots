# Evaluation Upgrade Plan

This pass moves the harness toward broad local-model evaluation without requiring paid APIs. The design assumption is that uncertainty is a first-class signal, not a later polish step.

## First-Round Foundation

- Local provider path: use the OpenAI-compatible adapter for `llama.cpp`; keep `echo` as the deterministic CI provider.
- Metric surface: citation F1, deterministic grounding, opt-in consistency variance, bootstrap mean CIs, score summaries, calibration helpers, and agreement helpers.
- Rubric surface: instruction format, constraint satisfaction, format compliance, negative constraints, and hedging calibration.
- Slice taxonomy: length, vagueness, citation need, factual grounding need, pedagogical prompts, retrieval-required prompts, strict-format prompts, and constraint-heavy prompts.
- Dependency tiers: core runtime stays small; LLM, NLP, statistics, reporting, and dev tooling are optional installs.

## Near-Term Implementation Lanes

1. **Grounding Dataset**
   - Add `data/prompts/grounding.jsonl` with context-bearing examples.
   - Populate `data/corpus/` with small source documents keyed by ID.
   - Extend the runner to resolve `context_ids` from corpus files.

2. **Suite-Aware Runner**
   - Add `--suite` to `src.runners.batch`.
   - Let suites select prompt files, expected metrics, rubrics, and default slice filters.
   - Keep `--prompts` as the low-level escape hatch.

3. **Uncertainty-Aware Gates**
   - Persist per-example metric vectors in a comparison-friendly artifact.
   - Use bootstrap intervals for baseline/candidate deltas.
   - Gate on whether regressions exceed uncertainty, not only raw mean drops.

4. **Local Model Run Profiles**
   - Add config presets for fast smoke runs, broad local runs, and expensive repeated-consistency runs.
   - Capture model name, quantization, server endpoint, temperature, max tokens, seed policy, and hardware notes in run metadata.

5. **Report Expansion**
   - Add worst-example tables per metric and slice.
   - Add baseline deltas and confidence intervals.
   - Add provider/run-profile metadata so local model results are interpretable.

6. **Optional Heavy Upgrades**
   - Replace lexical consistency with embedding similarity when `sentence-transformers` is installed.
   - Add Mann-Whitney U and effect sizes when `scipy` is installed.
   - Add human annotation import for agreement metrics.

## Capability Map

| Capability       | Current State                                        | Next Step                                                     |
| ---------------- | ---------------------------------------------------- | ------------------------------------------------------------- |
| Local providers  | OpenAI-compatible adapter configured for `llama.cpp` | Add run profiles and endpoint health checks                   |
| Citation eval    | Implemented                                          | Improve expected-source semantics beyond `[N]` count matching |
| Grounding eval   | Metric implemented                                   | Add context datasets and corpus resolution                    |
| Consistency eval | Opt-in metric implemented                            | Add suite/run-profile defaults                                |
| Rubrics          | Multi-family deterministic engine                    | Add richer per-prompt constraints from dataset metadata       |
| Slices           | Expanded heuristic taxonomy                          | Add failure-mode summary report                               |
| Statistics       | Bootstrap/calibration/agreement helpers              | Integrate uncertainty into gates                              |
| Reporting        | Dynamic summary tables                               | Add deltas, CIs, and worst examples                           |
