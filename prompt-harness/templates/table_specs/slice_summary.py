"""Generate dense slice-focused QA tables for anomaly scanning.

Placeholder producing static example until dynamic delta logic implemented.
"""
from __future__ import annotations

EXAMPLE_TABLE = """
| Slice      | Citations F1 | Instr Score | P95 Latency | Gate Status |
|------------|--------------|-------------|-------------|-------------|
| needs_citations | 0.42 ↑0.05 | 0.61 → | 820ms ↑40ms | PASS |
| length_short    | 0.38 ↓0.02 | 0.55 ↓0.03 | 760ms ↓10ms | PASS |
| constraint_heavy| 0.31 → | 0.58 ↑0.01 | 910ms ↑60ms | PASS |
""".strip()

def build_slice_gate_table(run_data=None):  # run_data placeholder
    return EXAMPLE_TABLE

__all__ = ["build_slice_gate_table", "EXAMPLE_TABLE"]
