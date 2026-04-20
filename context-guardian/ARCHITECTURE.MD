# Context Guardian Architecture

This document describes the high-level and detailed control flow of the VS Code Copilot `context-guardian` system. It shows input sources, processing modules, and runtime actions with concise ASCII diagrams.

---

## 1) High-level workflow

The system is organized as the sensor-processor-storage-correlation-action chain.

```
+----------------+     +----------------+     +----------------+     +------------------+
| Event sources   | --> | Preprocess     | --> | Storage        | --> | Correlate/Alert  |
| - Copilot ext   |     | (preprocessing)|     | (storage.psm1) |     | (correlate.py)   |
| - telemetry.ps1 |     +----------------+     +----------------+     +------------------+
+----------------+            |                     |                     |
                              v                     v                     v
                        +----------------+   +-----------------+     +----------------+
                        | Dedup & CSRF   |   | retention/trim  |     | mitigation     |
                        | & normalization|   | rotate          |     | (notification,  |
                        +----------------+   +-----------------+     | action scripts) |
                                                                          +----------------+
```

### Notes

- `copilot_sensor.py` and `telemetry.ps1` are sources.
- `preprocessing.psm1` is sanitization and normalization.
- `storage.psm1` persists and prunes data.
- `correlate.py` detects anomalies/decay events.

---

## 2) Telemetry ingestion

```
(1) Telemetry capture                                   (2) Stream transform
+---------------------+      +-----------------------+     +---------------------+
| telemetry.ps1       | ---> | copilot_sensor.py     | --> | preprocessing.psm1  |
+---------------------+      | - collect metrics     |     | - timestamp fixup   |
    |                        | - event buffers       |     | - field canonical   |
    +--> (record output)     +-----------------------+     | - dedupe by hash    |
                                                            +---------------------+
```

- telemetry sources include network state, process lists, WebSocket health, and memory footprint.
- preprocessor normalizes on a fixed schema so storage and correlate modules can operate deterministically.

---

## 3) storage + lifecycle

```
+----------------------+   +------------------------+   +---------------------------+
| preprocessing output  |-->| storage.psm1           |-->| maintenance loop          |
| (normalized frames)  |   | - write JSONL / blob  |   | - trim entries older than |
+----------------------+   | - hash key/integrity   |   |   retention days          |
                             | - retention policy     |   | - log rotation           |
                             +------------------------+   +---------------------------+
```

- `hashlib.psm1` utilities provide stable IDs and data fingerprinting.
- lifecycle includes periodic retention, file-based stop tokens, and a quick `status.json` snapshot.

---

## 4) Correlation and Alerting detail

```
+---------------------+   +----------------------+    +---------------------+   +----------------------+
| stored normalized   |-->| correlate.py         |--->| event scoring       |-->| action dispatcher    |
| frames              |   | - pattern matching   |    | - health risk score |   | - write alerts /     |
| (storage, time series)|   | - detail join       |    | - trending delta    |   |   run remediations   |
+---------------------+   | - decay heuristics   |    +---------------------+   +----------------------+
                            | - tool/extension     |    (e.g., Copilot memory +    |
                            |   interference flags |     websocket instability) |
                            +----------------------+
```

- correlation keys are computed from process signatures, URLs, and legacy context slots.
- output is an event feed consumed by dashboards or `comet-guardian.ps1` (future dedicated action module).

---

## 5) WIP: Comet Guard integration (drafted :check:)

Planned in this document once the Comet module is hardened.

- A conceptually similar problem of workspace hygiene and garbage control
- Comet health loop: ping, DNS, 443, GPU drop, memory spike, stop file.
- action signals into VS Code and external chat service guard.

---
