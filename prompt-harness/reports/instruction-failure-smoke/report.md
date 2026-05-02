# Run Report: instruction-failure-smoke
Generated: 2026-05-01T21:03:54.745793


## Global Stats
- **count**: 4
- **latency_p50_ms**: 29.9196500054677
- **latency_p95_ms**: 38.533149997965666
- **avg_input_tokens**: 119.0
- **avg_output_tokens**: 32.5
- **model**: echo-small
- **provider**: echo
- **constraint_satisfaction_mean**: 1.0
- **example_rubric_mean**: 0.2135
- **format_compliance_mean**: 0.0
- **hedging_calibration_mean**: 0.0
- **instruction_failure_defaults_mean**: 1.0
- **instruction_format_mean**: 0.4





## Slices
slice | count | avg constraint satisfaction score | avg example rubric score | avg format compliance score | avg hedging calibration score | avg instruction failure defaults score | avg instruction format score | avg latency ms
---|---:|---:|---:|---:|---:|---:|---:|---:
bullet_list | 2 | 1.0 | 0.0938 | 0.0 | 0.0 | 1.0 | 0.4 | 28.09
coherence_drift | 1 | 1.0 | 0.0 |  | 0.0 | 1.0 | 0.4 | 39.91
constraint_heavy | 4 | 1.0 | 0.0938 | 0.0 | 0.0 | 1.0 | 0.4 | 30.79
format_strict | 1 | 1.0 | 0.0 | 0.0 | 0.0 | 1.0 | 0.4 | 29.13
instruction_failure | 4 | 1.0 | 0.2135 | 0.0 | 0.0 | 1.0 | 0.4 | 31.7
instruction_retention | 2 | 1.0 | 0.3333 | 0.0 | 0.0 | 1.0 | 0.4 | 29.92
length_long | 4 | 1.0 | 0.2135 | 0.0 | 0.0 | 1.0 | 0.4 | 31.7
memory_recall | 1 | 1.0 | 0.0 |  | 0.0 | 1.0 | 0.4 | 39.91
safety_robustness | 1 | 1.0 | 0.6667 |  |  | 1.0 | 0.4 | 30.71
underspecified | 2 | 1.0 | 0.0 | 0.0 | 0.0 | 1.0 | 0.4 | 34.52
versioned_edit | 1 | 1.0 | 0.1875 | 0.0 |  | 1.0 | 0.4 | 27.05

