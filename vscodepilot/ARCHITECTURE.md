# VS Code Extension Architecture

## Dependency Model

This extension follows a **hybrid dependency model** to balance self-containment with flexibility:

### 1. Vendored Runtime Dependencies ✅

**What**: PowerShell modules directly invoked by the extension's TypeScript code.

**Location**: `lib/powershell/`

**Modules**:

- `jso-engine.psm1` - JSONL/JSON primitives
- `hashing-primitives.psm1` - Hash algorithm primitives
- `PSLinter.v2.psm1` - PowerShell static analysis and linting

**Why Vendored**:

- Extension code directly calls these via PowerShell bridge (`jso-blackbelt.ts`)
- Need guaranteed version compatibility
- Must be present for extension to function
- Bundled in VSIX package

**Import Pattern**:

```typescript
// TypeScript → PowerShell module (direct invocation)
const modulePath = path.join(
  __dirname,
  "..",
  "lib",
  "powershell",
  "jso-engine.psm1"
);
execSync(`pwsh -NoProfile -Command "Import-Module '${modulePath}'; ..."`);
```

### 2. External Host Dependencies ⚠️

**What**: PowerShell modules that produce artifacts (dump files) consumed by the extension.

**Location**: User's PowerShell profile location (NOT in extension)

**Modules**:

- `CyberneticConsole-Lite.psm1` (or any compatible console capture module)

**Why External**:

- Extension never imports or invokes this module
- Only depends on the dump file format it produces
- Lives in user's PowerShell profile environment
- User may customize/replace with compatible implementation
- Not bundled in VSIX package

**Contract Pattern**:

```typescript
// TypeScript → Dump files (indirect, format-based contract)
const dumpPath = path.join(
  process.env.COPILOT_GLOBAL_HOME,
  "dumps",
  "2025-11-17.jsonl"
);
const records = await readJsonlDump(dumpPath); // Just reads files, never imports module
```

## Architectural Principles

### Principle 1: Direct Invocation = Vendored

If TypeScript code directly calls a PowerShell module (via `Import-Module` in a spawned process), that module must be vendored in `lib/`.

**Example**: `jso-blackbelt.ts` spawns PowerShell and imports `jso-engine.psm1`.

### Principle 2: Format-Based Contract = External

If TypeScript code only depends on file formats (JSONL dumps, config files, etc.) produced by a module, that module can remain external.

**Example**: `safe-shell.ts` reads JSONL dumps but never imports the console module that writes them.

### Principle 3: One Canonical Source

External dependencies should have a single canonical source outside the extension:

- User's PowerShell profile imports from original source location
- Extension reads artifacts produced by that source
- No confusion about "which version is running"

### Principle 4: Contract Documentation

External dependencies require explicit contract documentation:

- File format schemas (see `ConsoleRecord` interface in `safe-shell.ts`)
- File location conventions (e.g., `$env:COPILOT_GLOBAL_HOME/dumps/`)
- Installation instructions for users
- Compatibility requirements

## Directory Structure

```
vscode-extension/
├── lib/                          # Vendored dependencies (bundled in VSIX)
│   ├── powershell/
│   │   ├── jso-engine.psm1      # ✅ Direct invocation → vendored
│   │   ├── hashing-primitives.psm1
│   │   └── PSLinter.v2.psm1     # ✅ Direct invocation → vendored
│   └── README.md                 # Dependency documentation
│
├── src/
│   └── toolbelt/
│       ├── jso-blackbelt.ts      # Invokes jso-engine.psm1 (vendored)
│       ├── ps-linter.ts          # Invokes PSLinter.v2.psm1 (vendored)
│       └── safe-shell.ts         # Reads JSONL dumps (external contract)
│
└── ARCHITECTURE.md               # This file

External (User's Environment):
~/.../CyberneticConsole-Lite.psm1  # ⚠️ Format contract → external
```

## Decision Rationale

### Why NOT Vendor Console Module?

1. **No Direct Invocation**: TypeScript never imports or calls it
2. **User Customization**: Users may prefer different console capture implementations
3. **Independent Evolution**: Console module can evolve without extension updates
4. **Single Source of Truth**: Avoids confusion about "which copy is running"
5. **Profile Integration**: Module is part of user's PowerShell environment, not extension runtime

### Why YES Vendor JSO Engine?

1. **Direct Invocation**: `jso-blackbelt.ts` spawns PowerShell and imports it
2. **API Coupling**: Extension depends on specific function signatures
3. **Version Stability**: Need guaranteed compatibility with extension code
4. **Self-Containment**: Extension must work without external setup (beyond console dumps)

## Testing Implications

### Unit Tests (Vendored Dependencies)

Mock or stub the PowerShell invocation in `jso-blackbelt.ts`:

```typescript
// Mock execSync to avoid actually calling PowerShell
jest.mock("child_process");
```

### Integration Tests (External Dependencies)

Provide test fixtures that simulate the dump file format:

```typescript
// Create test dump file matching ConsoleRecord schema
const testDump = "/tmp/test-2025-11-17.jsonl";
fs.writeFileSync(
  testDump,
  JSON.stringify({
    type: "cmd",
    timestamp: "2025-11-17T10:00:00.000Z",
    session: "test1234",
    seq: 1,
    command: "Get-Date",
    exit_code: 0,
  }) + "\n"
);
```

No need to test the actual console module—just verify the extension can parse its output format.

## Future Considerations

### If Console Module Becomes Tightly Coupled

If future features require direct invocation of the console module (e.g., `Initialize-Session`, `Get-SessionInfo`), revisit the vendoring decision.

**Decision Tree**:

- Does TypeScript call `Import-Module ConsoleModule`? → Vendor it
- Does TypeScript only read dump files? → Keep external

### If Multiple Console Implementations Emerge

If users create alternative console capture modules, document the **minimum contract** clearly:

- Required dump file schema (ConsoleRecord fields)
- Required file naming convention (YYYY-MM-DD.jsonl)
- Required location (`$env:COPILOT_GLOBAL_HOME/dumps/`)

The extension remains agnostic to implementation details beyond this contract.

---

**Last Updated**: 2025-11-17
**Related Docs**: `lib/README.md`, `src/toolbelt/safe-shell.ts` (contract documentation)
