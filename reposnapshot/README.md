# reposnapshot - Perplexity-Sonnet draft

A PowerShell 7.5+ toolkit for capturing structured, LLM-readable snapshots of source code repositories.

reposnapshot crawls a directory tree, applies gitignore-aware filtering, reads and normalizes file contents through a configurable processing pipeline, and writes the result as sharded, byte-indexed flat files designed for efficient random access by language model tooling. The output format is not a dump — it is a structured artifact built for targeted retrieval without requiring the consumer to ingest the entire repository.

---

## Why it exists

Working with LLMs on real codebases has a practical problem: sharing source context either means uploading many files, pasting large blobs, or asking the model to speculate about code it cannot see. reposnapshot solves this by producing a single portable artifact that captures the full repository with enough structure for a model to navigate it precisely. The `_tree.md` table of contents with byte offsets means any file's content can be retrieved directly without scanning the whole snapshot.

---

## Architecture

reposnapshot is organized as a set of composable PowerShell modules with distinct responsibilities:

### Discovery — `rs.core.crawler`

`DiscoveryCrawler` performs a BFS traversal of the target directory, building a directory graph. It skips symlinks and reparse points, records file sizes, and reads ignore files (`.gitignore`, `.snapignore`, or any configured names) at each directory node as part of the traversal. The result is a graph of directory nodes, each carrying its local ignore file contents and file list, ready for the ignore engine.

### Filtering — `rs.core.ignore`

`IgnoreEngine` implements gitignore semantics over the directory graph. It does not do simple glob-to-regex conversion. The pipeline is:

1. **Normalize** — clean and validate raw glob strings
2. **Coalesce** — merge ignore and negation patterns per node, eliminating self-annihilating pairs
3. **Walk** — propagate active ignore sets down the directory tree, inheriting from parent nodes
4. **Reduce** — discard exception patterns dominated by a deeper positive ignore rule
5. **Compile** — translate surviving globs to compiled `[regex]` objects, cached by pattern signature
6. **Prune** — remove directory nodes that are themselves ignored by an ancestor

An **executive override** mode bypasses the full pipeline and instead uses a user-supplied allowlist of glob patterns as an explicit inclusion set — useful for targeted snapshots of specific subtrees or file types.

### Processing — `rs.core.colonel` / `RunspaceManager`

`RunspaceManager` is a general-purpose parallel text processing engine. It is not specific to reposnapshot — it provides the execution infrastructure for any workload that can be expressed as a sequence of stateless `(Item, Config) → Item` processor functions applied to a batch of items.

**Processor model.** Processors are plain `.ps1` scripts with a positional `(Item, Config)` signature. They declare no outer functions and no `#Requires` directives, which allows them to be loaded directly as named functions into a `RunspacePool`'s `InitialSessionState`. This means processor code runs inside the pool with no file I/O at dispatch time.

**Execution kernel.** `chain-executor` is the single dispatch kernel loaded into every runspace — both serial and parallel. It receives a pre-compiled execution plan (an ordered list of processor steps with their configurations) and drives the chain loop. Because both pathways use the same kernel, the serial and parallel branches of the manager are structurally identical at the point of execution.

**Thread budgeting.** Worker count is resolved automatically from logical core count minus reserved cores, then graded down by a τ/K crossover heuristic: if the batch is too small to justify parallel overhead, dispatch falls back to a single runspace. All budgeting parameters (τ, K, reserved cores, explicit worker count) are tunable via the fluent API.

**Fluent API.** The manager is configured via method chaining:

```powershell
New-RunspaceManager
    .AddProcessorManifest(@{
        'file-read' = "$PSScriptRoot/processors/file-read.ps1"
        'format'    = "$PSScriptRoot/processors/format.ps1"
        'rs-psstrip' = "$PSScriptRoot/processors/rs-psstrip.ps1"
        'rs-indent'  = "$PSScriptRoot/processors/rs-indent.ps1"
    })
    .AddProcessingProfile(@('*.ps1', '*.psm1'))
        .AddProcessorStep('rs-psstrip', @{ Operations = @('block-comments','line-comments') })
        .AddProcessorStep('rs-indent',  @{ Operations = @('detab','min-indent-2'); TargetUnit = 2 })
    .Initialize('Core')
    .Run($items)
```

Glob-to-profile matching is resolved at planning time. Workers receive an immutable execution plan; no manifest lookups or planning logic runs inside a runspace.

**Note:** The fluent profile API and unified chain-executor dispatch are the active development direction. The current `rs.core.colonel` module implements the core runspace lifecycle and budgeting machinery; the planning and profile API are being designed in parallel.

### Processors

Stateless per-item transform scripts with a stable `(Item, Config) → Item` contract:

| Processor    | Purpose                                                                                                                                                                                                     |
| ------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `file-read`  | Reads file bytes, rejects binary (NUL byte check), decodes as UTF-8                                                                                                                                         |
| `format`     | Whitespace normalization: LF canonicalization, BOM removal, NFC, zero-width strip, trailing/inner space, blank line limiting                                                                                |
| `rs-psstrip` | PowerShell comment removal using the PS AST (`Parser::ParseInput`) with regex fallback for unparseable files; distinguishes block comments, doc strings, comment blocks, line comments, and inline comments |
| `rs-csstrip` | C# comment removal via regex; handles `/* */` block comments, `///` doc strings, `//` line and inline comments with span merging                                                                            |
| `rs-indent`  | Indentation normalization: common-indent stripping, tab expansion, GCD-based indent rescaling, tabification                                                                                                 |

### Output — sharding and format

Processed file records are serialized to pipe-delimited `.txt` shard files. Each row contains: `idx | path | {char_count word_count whitespace_ratio entropy} | length | content`. A `_tree.md` table of contents records byte offsets for each shard, enabling direct seek-based access to any file record without scanning the full output.

---

## Pipeline stages (reposnapshot)

reposnapshot uses `RunspaceManager` in two sequential stages:

1. **Ingest** — `file-read` → `format` applied to raw file paths from the filtered crawler graph
2. **Post-ingest** — domain-specific processors (`rs-psstrip`, `rs-csstrip`, `rs-indent`) applied to the in-memory IR from stage 1, with glob-based profile matching selecting the appropriate processor chain per file type

A top-level pipeline coordinator (**Admiral**, in design) will orchestrate these stages alongside crawler, ignore, and export into a single callable entry point.

---

## Output format and LLM use

Snapshot artifacts are designed for use as LLM context. The recommended access pattern is:

1. Fetch `_tree.md` to orient the directory structure and locate shard boundaries
2. Fetch the specific shard containing the file of interest
3. Use the pipe-delimited row format and byte offsets for targeted retrieval

The per-file attribute fields (entropy, whitespace ratio, compression ratio) provide signal about file character before reading content — useful for filtering or prioritizing files in a retrieval pipeline.

---

## Status

reposnapshot V3 is under active development. The core modules — crawler, ignore engine, processor scripts, and the runspace manager — are functional. The fluent profile API, Admiral pipeline coordinator, and JSONl export stage are in design. The LTS monolith (`RepoSnapshot-LTS.psm1`) is a prior working prototype used for personal snapshot generation; it is not the intended public interface.

---

## Requirements

- PowerShell 7.5 or later (7.6 required for `rs.core.colonel`)
- Windows (crawler uses `Win32_Processor` via CIM; cross-platform support not yet implemented)
