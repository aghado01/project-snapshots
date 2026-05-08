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
  - Hierarchical / nested GMM extensions (HMoG, HME, HDP) — future research, see docs/gmm-hierarchical-extensions.md

- .NET 10 LTS / C# SDK guidelines, shared `Directory.Build.props`, standalone primitive/application projects under `projects/`, root `artifacts/` output, and other performance or HPC hardening steps
- Codify performance enhancement guidelines to highlight opportunities in code
- vectorization
- zero alloc
- several other recurring themes
- analyze current codebase for examples to generalize the guidelines doc

- Smoke tests, compilation tests, unit tests for for csharp code

one of my immediate goals is to audit the current pwshspc src landscape and identify vestiges of the original plan which was powershell-first. its important to clean up things like this and make it clear where gaps are from the transition into my new C# -first orientation with this project. and by this i mean detexting broken references as well as implicit expectations of things from powershell orchestration or helper functions

WIP theory for admissibility in SPC

- canberra is positive and not likely admissible for distances against Potts spins
- same for minkowski p>2

add wasserstein-2 to metric library

add similarity measures library and project

samplers

- SW
- SGLD (eventually)
- Metropolis-Hastings
- Reverse Jump MCMC (BARS from matlab inspiration)

checkpointing + state engine

- general invariants
- review DL data structures such as how weight matrix is stored
- application-specific various essential choke points
  - e.g.

pending performance updates

- graph initialization O(n^2) cases
  - parallelism
  - vectorization

wasserstein in metrics is currently wasserstein 1 aka earth mover's distance. want to incorporate wasserstein 2 aka bures-wasserstein as another addition to the metric library

estimators

rename graphs to "neighborhoods"

Mutual KNN and MST shoudl be combined into a single implementation, with MST augmentation as an optional flag for MutualKNN

Solver in CentralTendency (Product manifold geometric median, frechet means) can be pulled out and used more generally. ProductManifold geometric median and frechet means can be separated as two separate partial classes or standalone static classes

Fréchet Distance
Wasserstein-2

bring in similarities from mathdig to hashish

e-ball - normalize ? connection to bandwidth estimators?

review geometric central tendency updates

move delta estimators to the new graph initialization

clustering primitives

- review matlab inspirations

finish GMM implementation

- clean up API surface
- recursive mixture models
- component merging strategies
  - tiling -> merge
