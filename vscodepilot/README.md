# VS Code Copilot Toolbelt Extension

**TypeScript tooling infrastructure for VS Code Copilot Chat integration**

A VS Code extension that provides custom chat tools for safe shell access, JSONL operations, code search, and PowerShell linting.

---

## ⚠️ Prerequisites

### 1. PowerShell Console Capture Module (Required)

This extension depends on a **PowerShell console capture module** (e.g., `CyberneticConsole-Lite.psm1`) that must be installed separately in your PowerShell profile.

**What it does**: Captures PowerShell terminal activity to JSONL dump files.

**Setup**: Add to your PowerShell profile (`Microsoft.PowerShell_profile.ps1`):

```powershell
# Import console capture module (adjust path to your installation)
Import-Module "C:\Path\To\CyberneticConsole-Lite.psm1" -Force
Initialize-CyberneticConsole -Scope Global
```

**Contract**: Must write JSONL files to `$env:COPILOT_GLOBAL_HOME/dumps/YYYY-MM-DD.jsonl` using the `ConsoleRecord` schema (see `src/toolbelt/safe-shell.ts` for details).

**Why external?**: The extension reads dump files but doesn't import the console module. This allows you to customize or replace the console implementation independently. See `ARCHITECTURE.md` for details.

### 2. Environment Variables

- `$env:COPILOT_GLOBAL_HOME` - Base directory for console dumps (e.g., `C:\Users\YourName\PortDenv`)

---

## 🚀 Quick Start

````powershell## 📁 Directory Structure

# 1. Setup toolbelt modules (copies from source repo)

.\setup-toolbelt.ps1```

toolbelt/

# 2. Install dependencies├── 📄 Core Modules (Active Implementation)

npm install│   ├── safe-shell.ts         - Gargoyle-resistant terminal commands

│   ├── safe-shell.d.ts       - Type definitions for safe-shell

# 3. Start development│   ├── jso-belt.ts           - JSONL/JSON utilities & PowerShell bridge

npm run watch│   └── copilot-toolbelt.ts   - Main export point (search, lint, scaffold)

│

# 4. Press F5 in VS Code to launch extension in debug mode├── ⚙️ Configuration

```│   ├── package.json          - Node.js dependencies

│   ├── tsconfig.json         - TypeScript compiler config

## 📦 Features│   └── copilot-tools-instructions.md - Usage guidelines for Copilot

│

Provides 20+ custom chat tools organized into modules:├── 📚 docs/ - Reference Documentation

│   ├── VSCODE-TOOL-INTEGRATION.md  - Primer on VS Code tool API

### Safe Shell (7 tools)│   ├── SAFE-SHELL-DESIGN.md        - Safe shell architecture

- `sendToInteractiveShell` - Execute PowerShell commands│   ├── SAFE-SHELL-CHEATSHEET.md    - Quick reference for safe-shell

- `getLastCommand` - Get most recent command result│   └── INTEGRATION-GUIDE.md        - Extension integration steps

- `readConsoleDump` - Read console history from JSONL│

- `getRecentConsoleRecords` - Recent console activity├── 📊 reports/ - Status & Planning

- `getErrorCommands` - Find failed commands│   ├── CONSOLIDATION-ANALYSIS.md   - Module analysis & decisions

- `getDumpInfo` - Get console dump metadata│   ├── SAFE-SHELL-SUMMARY.md       - Project summary

- `getTodaysDumpPath` - Get today's console dump path│   ├── SAFE-SHELL-INDEX.md         - Documentation index

│   ├── POWERSHELL-TOOLS-PLAN.md    - PowerShell tooling roadmap

### JSO Blackbelt (11 tools)│   ├── THIS-IS-BUILT.md            - Build status

- `streamJsonl` - Memory-efficient JSONL streaming│   └── Perplexity-safe-shell-access.md - Original design guidance

- `readJsonlWindow` - Paginated JSONL reading│

- `deduplicateJsonl` - Bloom filter deduplication├── 💬 .discussion/ - Design Discussions

- `splitJsonlWithProcessor` - Split JSONL files│   └── Perplexity-TS-Tools-discussion.md

- `getHash` - Content hashing (FNV-1a, SHA256)│

- `getRollingHashes` - Rolling hash computation└── 🗄️ .depr/ - Deprecated Code

- `canonicalJson` - Deterministic JSON serialization    ├── tool-ready.ts                    - Superseded by jso-belt exports

- `mergeJson` - JSON object merging    ├── Perplexity-TS-Toolbelt-full.ts   - Reference implementation

- `findEventsByHash` - Search events by hash    └── safe-interactive-shell.ts        - Superseded by safe-shell.ts

- `getRecentSupervision` - Recent supervision events```

- `JsonlBinaryIndex` - Binary indexing class

---

### Power Tools (4 tools)

- `rgAdvancedSearch` - Advanced ripgrep pattern search with rich match details
- `rgSearch` - Basic ripgrep pattern search (backwards compatible)
- `fdAdvancedList` - Advanced fd file listing with filtering
- `fdList` - Basic fd file listing (backwards compatible)

### PS-Linter (1 tool)

- `lintPowerShell` - PowerShell static analysis and linting
  - 9 custom rules for PS 7.5+ best practices
  - Profiles: Strict, Formatting, Repo, Gallery
  - Security: Detects Invoke-Expression, command injection
  - Structure: Parse error detection, header validation
  - Style: Indentation, function help, module manifests

---

## 🎯 Module Overview

### 1. **safe-shell.ts** ✅ Complete

## 🔧 Development

**Purpose**: Gargoyle-resistant shell access for PowerShell commands

### Build Commands

**Status**: Production-ready, fully documented

```powershell

# Compile once**Key Functions**:

npm run compile- `sendToInteractiveShell()` - Send command to terminal (fire-and-forget)

- `getLastCommand()` - Read last command result from JSONL dump

# Watch mode (recompile on changes)- `readConsoleDump()` - Query console history with filters

npm run watch- `getErrorCommands()` - Find failed commands

- `getTodaysDumpPath()` - Get current dump file path

# Build (alias for compile)

npm run build**Documentation**:

```- `docs/SAFE-SHELL-DESIGN.md` - Complete architecture

- `docs/SAFE-SHELL-CHEATSHEET.md` - Quick reference

### Debugging- `docs/INTEGRATION-GUIDE.md` - Extension integration steps



1. Start watch mode: `npm run watch`**Integration**: Ready for VS Code `vscode.chat.registerChatTool()` registration

2. Press **F5** in VS Code

3. A new "Extension Development Host" window opens---

4. Open Copilot Chat in the new window

5. Test tools### 2. **jso-belt.ts** ⚠️ Needs Refactoring



### Testing in Copilot Chat**Purpose**: JSONL processing, JSON utilities, PowerShell bridge



```**Status**: Core primitives complete, needs tool-shaped wrappers

@workspace Execute this command: Get-Date

```**Current State**:

- ✅ PowerShell bridge (`execJsoPowerShell`)

Copilot will automatically invoke the `sendToInteractiveShell` tool.- ✅ JSONL streaming (`streamJsonl`)

- ✅ Bloom filters, binary indexing

## 📁 Project Structure- ✅ Hash utilities

- ⏳ Need tool-shaped exports (see Todo #3)

````

vscode-copilot-toolbelt/**TODO**:

├── src/- Add `readJsonlWindow()` wrapper

│ ├── extension.ts # Main extension entry point- Add `canonicalJson()` wrapper

│ └── toolbelt/ # Copied from toolbelt repo- Add `getPathHash()` wrapper

│ ├── copilot-toolbelt.ts- Add `deduplicateJsonlTool()` wrapper

│ ├── jso-blackbelt.ts

│ ├── power-tools.ts**Documentation**: `QUICK-REFERENCE.md` (JSO belt operations)

│ ├── safe-shell.ts

│ ├── \*.d.ts (type definitions)---

│ └── jso-engine.psm1

├── out/ # Compiled JavaScript (generated)### 3. **copilot-toolbelt.ts** ❌ Stub

├── .vscode/

│ ├── launch.json # F5 debug configuration**Purpose**: Main export point for search, lint, scaffold tools

│ └── tasks.json # Build tasks

├── package.json**Status**: Minimal stub, needs implementation

├── tsconfig.json

├── setup-toolbelt.ps1 # Setup script**Planned Features**:

└── INIT-GUIDE.md # Detailed initialization guide- `rgSearch()` - ripgrep pattern search

```- `fdList()` - fd file listing

- `psLintTool()` - PSScriptAnalyzer wrapper

## 📦 Packaging & Installation- `psScaffoldTool()` - Plaster scaffolding wrapper

### Create Extension Package**TODO**: Implement per todo list items #4, #5, #6

````powershell---

npm run package

```## 🚀 VS Code Tool Integration



This creates `vscode-copilot-toolbelt-0.1.0.vsix`### What It Means



### Install ExtensionYou're preparing these TypeScript functions to be registered as **Copilot Chat Tools** using the VS Code Extension API.



```powershell**Flow**:

# Option 1: Use npm script```

npm run install-extUser types in Copilot Chat

    ↓

# Option 2: Manual installCopilot decides to invoke a tool

code --install-extension vscode-copilot-toolbelt-0.1.0.vsix    ↓

VS Code calls your registered handler

# Option 3: VS Code UI    ↓

# Extensions → ... menu → Install from VSIXYour TypeScript function executes

```    ↓

Result returned to Copilot

## 🛠️ Configuration    ↓

Copilot formats response for user

After installation, configure tool sets in VS Code settings:```



**File**: `User/prompts/copilot-toolbelt.toolsets.jsonc`### Registration Pattern



```jsonc```typescript

{import * as vscode from 'vscode';

  "safe-shell": {import * as safeShell from './toolbelt/safe-shell';

    "tools": ["sendToInteractiveShell", "getLastCommand"],

    "description": "Terminal command tools",export function activate(context: vscode.ExtensionContext) {

    "icon": "terminal"  context.subscriptions.push(

  }    vscode.chat.registerChatTool(

}      'sendShellCommand',

```      'Send a command to PowerShell terminal',

      async (request: vscode.ChatToolRequest) => {

See `src/toolbelt/docs/TOOLSETS-TEMPLATE.md` for complete examples.        const command = request.parameters.command as string;

        const result = await safeShell.sendToInteractiveShell({ command });

## 📚 Documentation

### Core Architecture
- **ARCHITECTURE.md** - Dependency model and design principles
- **lib/README.md** - Vendored vs. external dependencies explained

### Development Guides
- **INIT-GUIDE.md** - Complete initialization and development guide
- **src/toolbelt/docs/VSCODE-TOOL-INTEGRATION.md** - VS Code API guide
- **src/toolbelt/docs/TOOLSETS-TEMPLATE.md** - Tool sets configuration
- **src/toolbelt/docs/TOOLSETS-EXPLAINER.md** - Tool sets concept

### Module Documentation
- **src/toolbelt/safe-shell.ts** - External host contract documentation (see file header)
- **src/toolbelt/jso-blackbelt.ts** - PowerShell bridge and JSONL operations

---

### Phase 1: Mock Implementation (Current)

- 3 tools registered with mock implementations**See**: `docs/VSCODE-TOOL-INTEGRATION.md` for complete primer

- Testable in Copilot Chat

- Use for understanding the flow---

### Phase 2: Real Implementation (Next)## 📋 Integration Status

- Uncomment imports in `src/extension.ts`

- Replace mock implementations with real function calls### Ready to Register (safe-shell.ts)

- Test incrementally

| Tool Name | Function | Status |

### Phase 3: Complete Registration|-----------|----------|--------|

- Add remaining 17+ tools| `sendShellCommand` | `sendToInteractiveShell()` | ✅ Ready |

- Follow pattern in extension.ts| `getLastCommandResult` | `getLastCommand()` | ✅ Ready |

- Test each tool set| `readConsoleHistory` | `readConsoleDump()` | ✅ Ready |

## ⚠️ Troubleshooting### Needs Tool Wrappers (jso-belt.ts)

### "Cannot find module 'vscode'"| Tool Name | Function | Status |

Run `npm install` to install dependencies.|-----------|----------|--------|

| `readJsonlWindow` | Need wrapper | ⏳ Todo #3 |

### "Tool not found"| `canonicalJson` | Need wrapper | ⏳ Todo #3 |

Verify tool registration in `src/extension.ts` and that extension is activated.| `getPathHash` | Need wrapper | ⏳ Todo #3 |

### Import Errors### Needs Implementation (copilot-toolbelt.ts)

Run `.\setup-toolbelt.ps1` to copy toolbelt modules.

| Tool Name | Function | Status |

### Hot Reload|-----------|----------|--------|

After code changes, reload Extension Host window (Ctrl+R) while `npm run watch` is running.| `rgSearch` | Need implementation | ⏳ Todo #4 |

| `fdList` | Need implementation | ⏳ Todo #4 |

## 📝 License| `psLintTool` | Need implementation | ⏳ Todo #5 |

| `psScaffoldTool` | Need implementation | ⏳ Todo #6 |

MIT

---

## 🤝 Contributing

## 🛠️ Quick Start

This extension integrates tools from the copilot-toolbelt library located at:

`c:\Users\azriy\PortDenv\.suproot\cybernetic-copilot\src\toolbelt`### 1. Review Documentation

See toolbelt documentation for details on individual tools.```bash

# Understand what you're building

cat docs/VSCODE-TOOL-INTEGRATION.md

# Learn safe-shell API

cat docs/SAFE-SHELL-CHEATSHEET.md

````

### 2. Install Dependencies

```bash
cd toolbelt/
npm install
```

### 3. Compile TypeScript

```bash
npx tsc
```

### 4. Start Integration

Create `extension.ts` in your VS Code extension:

```typescript
import * as safeShell from "./tools/safe-shell";
import * as jsoBelt from "./tools/jso-belt";

export function activate(context: vscode.ExtensionContext) {
  // Register safe-shell tools first (they're complete)
  registerSafeShellTools(context);

  // Add JSO belt tools as you implement wrappers
  // registerJsoBeltTools(context);
}
```

**See**: `docs/INTEGRATION-GUIDE.md` for step-by-step instructions

---

## 📖 Documentation

### Core References

- **docs/VSCODE-TOOL-INTEGRATION.md** - What tool integration means, how it works
- **docs/SAFE-SHELL-DESIGN.md** - Safe shell architecture, API reference
- **docs/SAFE-SHELL-CHEATSHEET.md** - Quick reference for safe-shell
- **docs/INTEGRATION-GUIDE.md** - Extension integration steps
- **QUICK-REFERENCE.md** - JSO belt operations reference

### Status Reports

- **reports/CONSOLIDATION-ANALYSIS.md** - Module analysis
- **reports/SAFE-SHELL-SUMMARY.md** - Project summary
- **reports/SAFE-SHELL-INDEX.md** - Documentation index

### Planning

- **reports/POWERSHELL-TOOLS-PLAN.md** - PowerShell tooling roadmap
- **.discussion/Perplexity-TS-Tools-discussion.md** - Design discussions

---

## 🔄 Todo List

Current work items (see `<todoList>` in conversation):

1. ✅ **Consolidation Analysis** - Complete
2. ⏳ **Refine PowerShell bridge** - jso-belt.ts generic typing
3. ⏳ **Create tool-shaped wrappers** - jso-belt.ts exports
4. ⏳ **Implement search tools** - power-tools.ts (rg/fd)
5. ⏳ **PSScriptAnalyzer wrapper** - psLintTool()
6. ⏳ **Plaster scaffolding** - psScaffoldTool()
7. ⏳ **Consolidate exports** - copilot-toolbelt.ts main export
8. ⏳ **Update instructions** - copilot-tools-instructions.md
9. ⏳ **Package.json & tsconfig** - Verify build setup
10. ⏳ **Test harness** - test-toolbelt.ts
11. ⏳ **Architecture docs** - Update README (this file)
12. ⏳ **Clean up deprecated** - Move to .depr/

---

## 🎯 Next Steps

### Immediate (Today)

1. ✅ Run cleanup script to organize directory
2. ✅ Review `docs/VSCODE-TOOL-INTEGRATION.md`
3. ⏳ Start VS Code extension integration with safe-shell tools

### Short-term (This Week)

1. Implement tool wrappers in jso-belt.ts
2. Register jso-belt tools in extension.ts
3. Test end-to-end with Copilot Chat

### Medium-term (Next Sprint)

1. Implement search tools (rg/fd)
2. Implement PowerShell tools (lint/scaffold)
3. Complete test harness
4. Production deployment

---

## 🧪 Testing

### Manual Testing

1. Install extension in VS Code
2. Open Copilot Chat
3. Try: `@workspace send shell command to run Get-Date`
4. Verify tool is invoked and result is shown

### Unit Testing

```typescript
// test/tools.test.ts
import * as assert from "assert";
import { sendToInteractiveShell } from "../safe-shell";

test("sendToInteractiveShell returns immediately", async () => {
  const start = Date.now();
  const result = await sendToInteractiveShell({ command: "echo test" });
  const elapsed = Date.now() - start;

  assert.ok(result.ok);
  assert.ok(elapsed < 1000);
});
```

---

## 📝 Notes

### Gargoyle Resistance

The **safe-shell** module is designed to eliminate "gargoyle loops" (blocking indefinitely on terminal output):

- ✅ Commands sent to terminal with `sendText()` (fire-and-forget)
- ✅ Results read from JSONL dump files (durable artifacts)
- ✅ Never blocks VS Code UI thread
- ✅ Never waits on terminal state

### PowerShell Bridge

The **jso-belt** module uses `execSync` to call PowerShell:

- ✅ Not a gargoyle risk (synchronous is fine for fast operations)
- ✅ Returns immediately for hash/JSONL operations
- ⚠️ Use `sendToInteractiveShell` for long-running commands

### TypeScript Configuration

- **Target**: ES2020
- **Module**: CommonJS
- **Dependencies**: None external (except `@types/node`)
- **Build**: `npx tsc` compiles to JavaScript

---

## 🤝 Contributing

When adding new tools:

1. Implement function in appropriate module
2. Add JSDoc comments
3. Update this README
4. Add to `copilot-tools-instructions.md`
5. Register in extension.ts
6. Add tests

---

## 📄 License

(Project license goes here)

---

**Status**: ✅ Safe-shell complete, jso-belt needs wrappers, copilot-toolbelt needs implementation
**Last Updated**: 2025-11-16
**Next Action**: Register safe-shell tools in VS Code extension
