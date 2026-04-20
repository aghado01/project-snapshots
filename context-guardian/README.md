# Context Guardian

## Overview

`context-guardian` is a lightweight observability and remediation toolkit for agent-runtime stability. The project aims to enhance AI agent performance by monitoring and maintaining runtime environment homeostasis, guarding against resource and connectivity regressions, and mitigating known context accumulation issues (like VS Code Copilot retention/leakage).

Current capability focuses on runtime signal collection (`copilot_sensor.py`, `telemetry.ps1`), data normalization and storage (`preprocessing.psm1`, `storage.psm1`), and correlation logic (`correlate.py`). The Comet connection watch module is in progress under `src/comet-guardian.ps1` and will be documented separately.

## Current core modules (src)

### `copilot_sensor.py`

- collects telemetry from the Copilot/VS Code extension context.
- emits events or records used by correlation and storage layers.

### `correlate.py`

- implements outlier detection, correlation analysis as well as functionalities to support root-cause problem solving and enhance copilot coding assistant / agent
- can merge memory, resource and websocket events for incident scoring.

### `telemetry.ps1`

- PowerShell script for Windows runtime health capture (network, process, environment, service worker state).
- integrates with non-Python system checks and optionally emits JSON for downstream processing.

### `preprocessing.psm1`

- normalizes incoming telemetry streams.
- sanitizes timestamps, deduplicates context entries, and prepares data for indexing.

### `storage.psm1`

- lightweight storage abstractions for context data.
- includes read/write helpers, retention trimming, and optional CRC/hashing support.

### `hashlib.psm1`

- utility crypto/hashing functions used by storage layer.
- ensures stable ID generation for trace association and content-addressable entries.

### `extension/` (VS Code helpers)

- contains Copilot VS Code extension support files.
- `copilot-decay.ts` implements decay/tracking of in-memory context values.
- `sessionTelemetry.ts`, `turn_records.ts`, `writer.ts` support local agent context events.

## Usage

1. inspect the source modules under `src/`
2. invoke `telemetry.ps1` for initial environment capture
3. run `copilot_sensor.py` + `preprocessing.psm1` to generate normalized context records
4. use `correlate.py` to output alerts and insights
