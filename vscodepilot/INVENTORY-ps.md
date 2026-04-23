# PowerShell API Inventory

## jso-engine.psm1
- Resolve-CanonicalPath - Signature: `Resolve-CanonicalPath -Path <string>`
  Normalizes path, resolves relative/absolute paths with consistent separators and case.
- Get-PathHash - Signature: `Get-PathHash -Path <string>`
  Computes FNV1a hash for a filesystem path using canonical normalized path.
- Get-ContentHash - Signature: `Get-ContentHash -Content <string> [-WindowSize <int>]`
  Computes Rabin-Karp rolling hash for content (fingerprint of a string).
- Get-ContentChunks - Signature: `Get-ContentChunks -Content <string> [-WindowSize <int>] [-MinChunkSize <int>] [-MaxChunkSize <int>]`
  Finds content-defined chunk boundaries in large data streams.
- Find-StringPattern - Signature: `Find-StringPattern -Content <string> -Pattern <string>`
  Rabin-Karp string pattern search on text.
- New-JsoEncoder - Signature: `New-JsoEncoder -Type <string> [-Options <hashtable>]`
  Factory for JSON/JSONL encoder objects (minify/canonical/jsonpath/tabular encoders).
- New-BloomFilter - Signature: `New-BloomFilter -ExpectedItems <int> [-FalsePositiveRate <double>]`
  Creates a Bloom filter object with expected items and false positive rate.
- Add-BloomFilterItem - Signature: `Add-BloomFilterItem -BloomFilter <object> -Item <string>`
  Adds an item to Bloom filter.
- Test-BloomFilterItem - Signature: `Test-BloomFilterItem -BloomFilter <object> -Item <string>`
  Tests membership in Bloom filter (probabilistic).
- Get-BloomFilterHashes - Signature: `Get-BloomFilterHashes -BloomFilter <object>`
  Retrieves low-level hash state from Bloom filter.
- Get-JsonlEnumerator - Signature: `Get-JsonlEnumerator -Path <string> [-Skip <int>] [-Take <int>]`
  Enumerates JSONL file records (streamed as PowerShell objects).
- New-JsonlBinaryIndex - Signature: `New-JsonlBinaryIndex -JsonlPath <string> [-IndexPath <string>]`
  Builds binary index for JSONL for random line access.
- Read-JsonlBinaryIndex - Signature: `Read-JsonlBinaryIndex -JsonlPath <string> -IndexPath <string> -LineNumber <int>`
  Reads an indexed JSONL line using binary index.
- ConvertTo-CanonicalJson - Signature: `ConvertTo-CanonicalJson -InputObject <object>`
  Converts object to canonical deterministic JSON string.
- Get-FileSha256 - Signature: `Get-FileSha256 -Path <string>`
  Computes SHA256 hash of file contents.
- Merge-JsonObjects - Signature: `Merge-JsonObjects -Base <object> -Override <object>`
  Merges two JSON objects with priority.
- Get-JsonDepth - Signature: `Get-JsonDepth -InputObject <object>`
  Computes nested object depth.
- Get-ContentChunks - Finds content-defined chunk boundaries for large data streams.
- Find-StringPattern - Rabin-Karp string pattern search on text.
- New-JsoEncoder - Factory for JSON/JSONL encoder objects (minify/canonical/jsonpath/tabular encoders).
- New-BloomFilter - Creates a Bloom filter object with expected items and false positive rate.
- Add-BloomFilterItem - Adds an item to Bloom filter.
- Test-BloomFilterItem - Tests membership in Bloom filter (probabilistic).
- Get-BloomFilterHashes - Retrieves low-level hash state from Bloom filter.
- Get-JsonlEnumerator - Enumerates JSONL file records (streamed as PowerShell objects).
- New-JsonlBinaryIndex - Builds binary index for JSONL for random line access.
- Read-JsonlBinaryIndex - Reads an indexed JSONL line using binary index.
- ConvertTo-CanonicalJson - Converts object to canonical deterministic JSON string.
- Get-FileSha256 - Computes SHA256 hash of file contents.
- Merge-JsonObjects - Merges two JSON objects with priority.
- Get-JsonDepth - Computes nested object depth.

## agent-linter-ps.psm1
- PSUseRequiresVersion75 - Rule check for #Requires version in script.
- PSHeaderOrderConvention - Rule check for proper header order in PowerShell scripts.
- PSProvideFunctionHelpExtended - Ensures functions have .SYNOPSIS/.DESCRIPTION help.
- PSModuleManifestMetadata - Verifies module manifest metadata for publication.
- PSAvoidParallelWithoutThrottle - Ensures parallel loops use throttle limit.
- PSAvoidDangerousInvocation - Detects dangerous patterns like Invoke-Expression.
- PSAvoidInterpolatedCommand - Detects interpolated command generation security risks.
- PSDetectInvisibleCharacters - Detects unicode zero-width and bidi chars in scripts.
- PSDetectStructuralLexingErrors - Detects unbalanced braces/quotes/comments in tokens.
- Invoke-PSLinter - Unified entry function for running heuristic lint profile.

## parallel-engine-v2.psm1
- parfor - Executes a parallel for loop (iteration count based) via engine.
- parforeach - Executes a parallel foreach loop over an item collection.
- parrange - Executes a parallel range loop (array or tuple bounds).
- parwhile - Executes a parallel while loop until condition false.
- paruntil - Executes a parallel until loop until condition true.
- Invoke-ParallelBatchProcessing - Core parallel batch worker for job groups.
- Invoke-ParallelJobWithSignal - Runs a job and writes status/result signal files for supervisor.

## supervisor-host.psm1
- Initialize-CopilotWorker - Bootstraps supervisor host environment and worker state.
- Start-JsonRpcLoop - Starts JSON-RPC loop listening for tool requests.
- Invoke-RpcHandler - Handles an individual RPC request and dispatches tool methods.
- Start-JobWorker - Launches a worker process for parallel jobs.
- Invoke-RipgrepSearch - Wrapper around ripgrep for fast code search.
- Invoke-FdList - Wrapper around fd file discovery.
- Read-JsonlWindow - Reads a window from JSONL file (used by remote tools).
- Invoke-PsLint - Calls PS Linter pipeline and returns diagnostics.

---
This inventory captures the main exported functions and their intent for PowerShell tooling in the Copilot extension.
