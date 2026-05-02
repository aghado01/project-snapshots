# Changelog

## 2026-05-01

### Added

- Explicit PowerShell-based Python workflow scripts: `scripts/resolve_python.ps1`, `scripts/python.ps1`, `scripts/install_deps.ps1`, `scripts/run_tests.ps1`, and `scripts/refresh_model_inventory.ps1`.
- GGUF model inventory generation and a tracked registry at `data/model_inventory.json`.
- Workflow documentation in `docs/workflow-reference.md` and contributor/assistant orientation in `AGENTS.md`.
- Self-hosted Windows GitHub Actions workflow for scheduled and on-demand model inventory refresh.
- Single local `.venv` bootstrap via `scripts/ensure_venv.ps1` with requirement fingerprint checking.
- Python-native context probe generation under `src/probes` with payload artifacts and harness JSONL fixture output.
- Probe suite config at `configs/probe_suites.yaml` and a `scripts/generate_probe_payloads.ps1` orchestration entrypoint.
- Mercury-inspired multi-turn instruction-failure fixtures under `data/prompts/instruction_failure/`.
- Per-example rubric criteria support for instruction-failure fixtures.
- Instruction-failure workflow documentation in `docs/instruction-failure-workflow.md`.
- Background local automation launcher and status reader under `scripts/start_background_run.ps1` and `scripts/get_background_run.ps1`.

### Changed

- Updated local automation and VS Code task commands to use the explicit Python wrapper instead of relying on bare `python` or editor interpreter state.
- Updated CI to run installs, tests, baseline generation, candidate runs, and gate comparison through the PowerShell wrapper scripts.
- Hardened baseline comparison and inventory workflow logic to better handle changes and repository commits.
- Simplified dependency setup away from selectable local dependency tiers and toward one canonical development environment.
- Preserved template-rendered probe payloads for line-indexed suites so invalid JSON and raw escaping boundaries can be generated intentionally.
- Extended OpenAI-compatible provider requests to accept structured chat messages from multi-turn fixtures.
- Changed VS Code tasks to launch background runs that write status and logs under `reports/runs/` instead of holding task terminals open for the full command.

### Fixed

- Resolved a circular import in the render package by moving report exports behind environment helper setup.
- Fixed relative gate comparisons so baseline metrics work for both `global.*` and direct metric paths.
- Fixed report rendering to humanize metric labels such as `avg_latency_ms` in generated markdown output.
- Fixed template autoescaping for extension-style template names such as `html`.
- Fixed `src/render/engine.py` loader typing and template registration logic so static analysis passes cleanly.
- Fixed the instruction-failure default non-empty-answer rubric regex so non-empty model outputs score correctly.

### Validated

- Verified Python resolution through the portable environment wrapper.
- Refreshed the local GGUF inventory from `D:\models\gguf` and wrote model metadata successfully.
- Ran the test suite through `scripts/run_tests.ps1` with final result: `54 passed`.
- Ran the instruction-failure echo smoke successfully; corrected default rubric summary now reports `instruction_failure_defaults_mean: 1.0`.
- Checked diagnostics after the final render engine updates with no remaining reported errors.
