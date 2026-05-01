# TODO

Not in any particular order.

- resolve and messy namespace and filename conventions

- Why is basically everything "SpcBatch"? If its so ubiquitous it doesn't need to annoted in every single #_R_ filename. do not care for `SpcBatch` everything because what else is there?
- Continue auditing metric/proximity separation now that metrics are static pairwise primitives and `spc.graph.cs` binds them into SPC graph initialization.

- Hashlib adaptations for hashing algorithms like simhash, minhash, others + lessons learned from recent mathdig work on writing cs implementations for Dotnet
- `src/hashish` directory
  - Currently working on some C# hash algorithm code,stay tuned.

- Expand supervisor-facing thermodynamic analysis over checkpoint history: chi, KL, Fisher-in-T, and handoff readiness signals
- Synthetic data experimental harness for testing systematically
- break synthetic data classes out in to partial classes with flexible dispatch as with analysis and spcbatch
- e.g. for each 'coin' there is a partial class cs file with orchestration/dispatch living in the src/ root directory
- same for hashing primitives which are under development as mentioned above

- GMM implementation and principled optional handoff from SPC-initialized clusters

- .NET 10 LTS / C# SDK guidelines, shared `Directory.Build.props`, standalone primitive/application projects under `projects/`, root `artifacts/` output, and other performance or HPC hardening steps
- Smoke tests, compilation tests, unit tests for for csharp code

one of my immediate goals is to audit the current pwshspc src landscape and identify vestiges of the original plan which was powershell-first. its important to clean up things like this and make it clear where gaps are from the transition into my new C# -first orientation with this project. and by this i mean detexting broken references as well as implicit expectations of things from powershell orchestration or helper functions

WIP theory for admissibility in SPC

- canberra is positive and not likely admissible for distances against Potts spins
- same for minkowski p>2

checkpointing for things like graph initialization

still need to do the parallel update for graph initialization to offset the O(n2)

wasserstein in metrics is currently wasserstein 1 aka earth mover's distance. want to incorporate wasserstein 2 aka bures-wasserstein as another addition to the metric library
