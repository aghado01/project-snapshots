# Conversational Quality Lab - Live Demo

## 5-Minute Demo Flow
1. **Setup**: `./init.ps1` → environment ready
2. **Baseline**: `scripts/run_baseline.ps1` → evaluate current model
3. **Candidate**: `python -m src.runners.batch --out reports/candidate --prompts data/prompts`
4. **Compare**: `pwsh scripts/compare_gates.ps1 -Candidate reports/candidate` → gate-aware diff
5. **Results**: Open `reports/candidate/report.md` → slice-aware quality metrics

## Key Talking Points
- Slice-aware evaluation (length, ambiguity, constraints, citation-needs)
- Regression gates (quality thresholds in CI)
- Deterministic reproducibility (seed = base + index)
- Extensible rubric engine (instruction format now, add more later)
- Baseline alias symlink `reports/baseline-latest`

## Optional Extensions (Mention Only)
- Delta-based gates (relative degradation thresholds)
- LLM-as-judge layered scores
- Cost & token efficiency metrics
- HTML rich dashboard export
