# JSO Tool Usage Guidelines for Models

These guidelines are for models/agents using the `jso-*` and `claude-jso-*` PowerShell tools to inspect Claude Code JSONL exports without flooding the console or chat context.

## Core Principles

1. Prefer targeted probes over full dumps.
   Start with counts, schema, structure, and path stats before printing records. Use the smallest command that answers the question.

2. Keep large results in files.
   If a command can return many rows or large nested JSON, write the result to a temp artifact and return only the path plus a short summary.

3. Preview before expanding.
   Use preview/windowing modes for record inspection. Only fetch full records after identifying the exact line, path, or field that matters.

4. Preserve JSONL shape.
   Do not manually parse JSON with regex or string splitting when a JSO path/schema/read primitive exists. Use the JSONL utilities so paths, arrays, and escaping are handled consistently.

5. Treat tool files as layered.
   - `jso-hash.ps1`: hashing/string primitives only.
   - `jso-jackson.ps1`: JSONL primitives, indexes, previews, Bloom filters, hash sidecars.
   - `jso-debug.ps1`: user-facing inspection/search/compare/validate/profile workflows.
   - `claude-jso-*.ps1`: Claude-specific export, grouping, and markdown stages.

## Setup

Dot-source only the layers needed for the task. The files are intentionally independently dot-sourceable.

```powershell
. "$env:CLAUDE_CONFIG_DIR\tools\jso-hash.ps1"
. "$env:CLAUDE_CONFIG_DIR\tools\jso-jackson.ps1"
. "$env:CLAUDE_CONFIG_DIR\tools\jso-debug.ps1"
```

For Claude export/grouping workflows:

```powershell
. "$env:CLAUDE_CONFIG_DIR\tools\claude-jso-run.ps1"
```

## Inspection Workflow

Use this order unless there is a clear reason not to:

1. Count or validate the file.

```powershell
Get-JsonlRecordCount -Path $path
Test-Jsonl -Path $path
```

2. Learn the shape.

```powershell
Format-JsonlSchema -Path $path | Format-Table -AutoSize
Show-JsonlStructure -Path $path -At 0 | Format-Table -AutoSize
```

3. Measure the field before previewing it.

```powershell
Get-JsonlPathStats -Path $path -JsonPath 'records[].text'
```

4. Preview a selected record.

```powershell
Format-JsonlRecord -Path $path -At 42 -Preview -PreviewMode Sandwich -MaxFieldChars 400
```

5. Use a window when a prior search gives an offset or area of interest.

```powershell
Format-JsonlRecord -Path $path -At 42 -PreviewWindow @{ Start = 1200; Length = 500 }
```

## Search Workflow

Prefer path-aware search when the target is a field value:

```powershell
Find-JsonlByPath -Path $path -JsonPath '_xid' -Equals 'thread-0001'
Find-JsonlByPath -Path $path -JsonPath 'records[].text' -Matches 'timeout|exception'
```

Use raw substring search when looking for an ID or exact text anywhere in the line:

```powershell
Find-JsonlById -Path $path -Id $uuid
Find-JsonlRecord -Path $path -Containing 'needle text'
```

Use predicate search only when path-aware search is not expressive enough:

```powershell
Find-JsonlRecord -Path $path -Where { $_.type -eq 'assistant' -and $_.message.id }
```

## Compare and Validate Workflow

Use hash sidecars for fast repeated comparisons:

```powershell
Compare-JsonlByHash -PathA $a -PathB $b -Rebuild
```

Use record-by-record comparison when you need the actual line payload:

```powershell
Compare-JsonlFiles -PathA $a -PathB $b
```

Use schema comparison for drift:

```powershell
Compare-JsonlSchemas -PathA $a -PathB $b -CoverageTolerance 1
```

## File-Oriented/RPC-Style Usage

For model workflows, avoid dumping large command output into the console. Prefer this artifact pattern:

```text
$env:CLAUDE_CONFIG_DIR\tmp\rpc\<timestamp>-<task>\
  request.json
  result.jsonl
  summary.json
  errors.jsonl
  stdout.txt
```

The model should return only:

- `result.jsonl` path
- `summary.json` path
- row count
- a 1-5 row preview if useful
- any error path if failures occurred

Recommended result formats:

- JSONL for streams of objects or rows.
- JSON for one structured object.
- CSV only when the result is flat and intended for spreadsheet-style inspection.
- TXT only for human-readable formatted output.

## Temp Artifact Rules

Use `$env:CLAUDE_CONFIG_DIR\tmp\rpc` as the default root for model/RPC-style generated artifacts.

Suggested directory names:

```powershell
$jobDir = Join-Path $env:CLAUDE_CONFIG_DIR ("tmp\rpc\{0}-{1}" -f (Get-Date -Format 'yyyyMMdd_HHmmss'), 'find-jsonl')
```

## RPC File Conventions

One tool call gets one job directory:

```text
$env:CLAUDE_CONFIG_DIR\tmp\rpc\YYYYMMDD_HHmmss-<command-or-task>\
```

Use lowercase kebab-case for `<command-or-task>`:

```text
20260429_153012-find-jsonl-by-path
20260429_153145-compare-jsonl-schemas
20260429_153300-preview-record-42
```

If multiple jobs can start in the same second, append a short unique suffix:

```text
20260429_153012-find-jsonl-by-path-a7f3
```

Use these stable filenames inside the job directory:

| File           | Required           | Purpose                                                                                              |
| -------------- | ------------------ | ---------------------------------------------------------------------------------------------------- |
| `request.json` | Yes                | Command name, arguments, working directory, timestamp, and model/tool metadata.                      |
| `result.jsonl` | For row streams    | One JSON object per output row. Default for search, compare, validate, profile, and duplicate scans. |
| `result.json`  | For single objects | One structured result object when the command naturally returns a single object.                     |
| `result.txt`   | For formatted text | Human-readable output that should not be parsed as data.                                             |
| `summary.json` | Yes                | Counts, status, elapsed time, result file paths, and a small preview.                                |
| `errors.jsonl` | If errors occur    | One JSON object per error, warning, or skipped malformed row.                                        |
| `stdout.txt`   | Optional           | Captured console text if a command writes host output.                                               |
| `stderr.txt`   | Optional           | Captured native/process error text when relevant.                                                    |

Prefer `result.jsonl` unless there is a strong reason to use another result file. Do not create multiple primary result files for the same job; if alternate formats are useful, name them explicitly, such as `result.csv` or `preview.txt`, and list them in `summary.json`.

Minimum `request.json` shape:

```json
{
  "command": "Find-JsonlByPath",
  "arguments": {
    "Path": "C:\\path\\thread.jsonl",
    "JsonPath": "records[].text",
    "Matches": "timeout|exception"
  },
  "workingDirectory": "C:\\Users\\azrie\\.claude",
  "startedAt": "2026-04-29T15:30:12Z"
}
```

Minimum `summary.json` shape:

```json
{
  "status": "ok",
  "rowCount": 128,
  "resultPath": "C:\\Users\\azrie\\.claude\\tmp\\rpc\\20260429_153012-find-jsonl-by-path\\result.jsonl",
  "errorPath": null,
  "startedAt": "2026-04-29T15:30:12Z",
  "endedAt": "2026-04-29T15:30:14Z",
  "elapsedMs": 1842,
  "preview": [{ "_index": 42, "kind": "match" }]
}
```

Use `status: "ok"`, `"partial"`, or `"error"`. A partial status means the command produced usable output but also wrote warnings/errors.

Write UTF-8 without BOM when creating text artifacts.

Keep intermediate files unless the user explicitly asks to clean them up. These files are useful for follow-up inspection and reproducibility.

## Console Discipline

Do not print full records by default.

Good console output:

```text
ResultPath: C:\Users\...\.claude\tmp\rpc\...\result.jsonl
SummaryPath: C:\Users\...\.claude\tmp\rpc\...\summary.json
Rows: 128
Preview: first 3 rows shown
```

Avoid console output like:

```text
<full JSONL dump of hundreds of records>
```

## Safety Checks

Before trusting a generated or edited JSONL file:

```powershell
Test-Jsonl -Path $path
Get-JsonlRecordCount -Path $path
```

After removing or rewriting records, rebuild any matching index sidecar:

```powershell
[void][JsonlIndex]::Build($path, [System.IO.Path]::ChangeExtension($path, '.jidx'))
```

When a command depends on hashing or substring search, ensure `jso-hash.ps1` is dot-sourced first.

## Choosing the Right Tool

- Need one record: `Get-JsonlRecord` or `Format-JsonlRecord -Preview`.
- Need nearby context: `Get-JsonlContext`.
- Need shape: `Format-JsonlSchema` or `Show-JsonlStructure`.
- Need path values: `Select-JsonlPath`.
- Need field search: `Find-JsonlByPath`.
- Need raw text/UUID search: `Find-JsonlRecord -Containing` or `Find-JsonlById`.
- Need duplicate detection: `Find-JsonlDuplicates`.
- Need size profile: `Measure-Jsonl`, `Get-JsonlSizeProfile`, or `Get-JsonlPathStats`.
- Need fast diff: `Compare-JsonlByHash`.
- Need exact line diff: `Compare-JsonlFiles`.
- Need schema drift: `Compare-JsonlSchemas`.

## Model Response Pattern

When using these tools, a model should answer with:

1. What was inspected.
2. Which artifact files were written, if any.
3. The small set of findings that matter.
4. The exact next command only when it is useful.

Do not paste large raw JSON unless the user explicitly asks for it.
