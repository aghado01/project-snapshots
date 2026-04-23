# TypeScript API Inventory

## copilot-toolbelt.ts
- **readJsonlWindow<T>(filePath: string, skip?: number, take?: number): Promise<T[]>**
  Reads a window of JSONL records from a file, paginated for large files.
- **canonicalJson(obj: any): string**
  Returns a deterministic, canonical JSON string for an object.
- **mergeJson(a: any, b: any): any**
  Shallow merges two JSON objects, with `b` taking precedence.
- **findEventsByHash(filePath: string, hash: string): Promise<any[]>**
  Finds events in a JSONL file matching a given hash.
- **getRecentSupervision(sessionLogPath: string, maxResults?: number): Promise<any[]>**
  Returns recent supervision events from a session log.

## jso-blackbelt.ts
- **streamJsonl<T>(options: JsonlStreamOptions): AsyncGenerator<T>**
  Streams JSONL records efficiently, supports skip/take for pagination.
- **JsonlBinaryIndex**
  Class for fast random access to JSONL files using a binary index.
- **BloomFilterClient**
  Probabilistic deduplication for large streams; add/test items efficiently.
- **HashUtils**
  Utilities for FNV-1a, SHA256, and rolling hashes.
- **JsonUtils**
  Canonicalization, merging, and depth calculation for JSON objects.
- **JsoProcessor**
  High-level orchestration for large-scale JSONL ETL workflows.
- **deduplicateJsonl, getHash, getRollingHashes, splitJsonlWithProcessor**
  Various helpers for deduplication and processing.
- **Types:**
  `BloomFilter`, `BloomFilterOptions`, `EncoderOptions`, `EncoderType`, `JsonlBinaryIndexOptions`, `JsonlIndexEntry`, `JsonlStreamOptions`, `JsoProcessorConfig`, `ProcessResult`, `ShardingOptions`

## power-tools.ts
- **fdList(args: FdListArgs): string[]**
  Lists files matching a pattern using fd.
- **fdAdvancedList(args: FdAdvancedListArgs): FdResult[]**
  Advanced file discovery with metadata.
- **rgSearch(args: RgSearchArgs): RgHit[]**
  Searches files for patterns using ripgrep, returns matches.
- **rgAdvancedSearch(args: RgAdvancedSearchArgs): RgDetailedHit[]**
  Advanced search with detailed match info.
- **Types:**
  `FdListArgs`, `FdAdvancedListArgs`, `FdResult`, `RgSearchArgs`, `RgHit`, `RgAdvancedSearchArgs`, `RgDetailedHit`

## linter-ps.ts
- **lintPowerShell(args: PSLintArgs): PSLintResult[]**
  Lints PowerShell scripts using PSLinter.v2, returns diagnostics.
- **formatLintResults(results: PSLintResult[]): string**
  Formats lint results for display.
- **Types:**
  `PSLintArgs`, `PSLintResult`

## safe-shell.ts
- **sendToInteractiveShell(args: ShellCommandArgs): Promise<ShellCommandResult>**
  Sends a command to the interactive PowerShell terminal (fire-and-forget).
- **readConsoleDump(args: ConsoleReadArgs): Promise<ConsoleRecord[]>**
  Reads console dump records from JSONL files.
- **getTodaysDumpPath(): string**
  Returns the path to today's console dump file.
- **getDumpInfo(): ConsoleDumpInfo**
  Returns info about the current dump file.
- **getLastCommand(), getErrorCommands(), getRecentConsoleRecords()**
  Helpers for querying command history and errors.
- **Types:**
  `ShellCommandArgs`, `ShellCommandResult`, `ConsoleReadArgs`, `ConsoleRecord`, `ConsoleDumpInfo`

## parallel-tools.ts
- **initialize(extensionPath: string): Promise<void>**
  Initializes the parallel tools system and supervisor.
- **startParallelJob(operation: string, options: ParallelJobOptions): Promise<string>**
  Starts a parallel PowerShell job asynchronously, returns job ID.
- **checkJobStatus(jobId: string): Promise<JobStatus | null>**
  Checks the status of a running job.
- **pollJobUntilComplete(jobId, onProgress?): Promise<JobStatus>**
  Polls a job until completion, with optional progress callback.
- **getJobResults<T>(jobId, keepFiles?): Promise<T[] | null>**
  Retrieves results from a completed job.
- **cancelJob(jobId: string): Promise<boolean>**
  Cancels a running job.
- **cancelAllJobs(): number**
  Cancels all running jobs.
- **getJobInfo(jobId: string): ParallelJob | null**
  Gets info about a specific job.
- **getAllJobs(): ParallelJob[]**
  Returns all tracked jobs.
- **getJobsByStatus(status: JobStatus): ParallelJob[]**
  Returns jobs filtered by status.
- **parfor(count, scriptBlock, options?): Promise<string>**
  Parallel for loop (PowerShell parfor).
- **parforeach(items, scriptBlock, options?): Promise<string>**
  Parallel foreach loop (PowerShell parforeach).
- **parrange(range, scriptBlock, options?): Promise<string>**
  Parallel range loop (PowerShell parrange).
- **parwhile(condition, scriptBlock, options?): Promise<string>**
  Parallel while loop (PowerShell parwhile).
- **paruntil(condition, scriptBlock, options?): Promise<string>**
  Parallel until loop (PowerShell paruntil).
- **Types:**
  `JobStatus`, `ParallelJob`, `ParallelJobOptions`, `JobProgress`, `ProgressCallback`

## structural-linter.ts
- **lintTypeScript(args: LintTypeScriptOptions): StructuralLintResult[]**
  Lints TypeScript code for structural/syntax errors.
- **detectUnbalancedBraces(code: string): { balanced: boolean, message?: string }**
  Checks for unbalanced braces in code.
- **formatStructuralLintResults(results: StructuralLintResult[]): string**
  Formats structural lint results for display.
- **Types:**
  `LintTypeScriptOptions`, `StructuralLintResult`

---
This inventory covers the main exported APIs, types, and their purposes for the VSCode Copilot Toolbelt extension.
