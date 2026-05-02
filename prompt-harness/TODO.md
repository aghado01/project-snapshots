~~No real prompt datasets — data/prompts/\*.jsonl are empty stubs. Without actual prompt sets, the whole pipeline is untestable end-to-end by anyone reviewing it. This is the biggest gap — you need even 20-30 real prompts in citations.jsonl and instructions.jsonl .~~ There are some prompts but they could be expanded and tuned to more current target audience interests

No real provider — providers.yaml only has echo-small and the real provider entries are commented out . For a demo you don't need live API keys in the repo, but you should add an OpenAIProvider stub with the correct interface so a reviewer can see the extension point is real.

mercury.py and src/metrics/mercury.py are empty — gates_mercury.yaml references Mercury-specific gates but the module has zero content . Either implement the stub or remove the config so there's no dead reference.

No tests — tests/unit/test_placeholders.py is literally assert True . For Khan, who explicitly values "careful, iterative, data-driven" quality, having zero tests is a red flag. You don't need full coverage — just test_citations.py, test_rubrics.py, and test_gates.py with a handful of cases each.

No README with a real runbook — the current README is one line: "Evaluation-first skeleton with batched runs, slice metrics, and regression gates." A reviewer needs to be able to clone → install → run baseline → compare → read report in under 5 minutes. That's what sells it.

update_baseline_alias.ps1 is empty — broken script in the main workflow.
