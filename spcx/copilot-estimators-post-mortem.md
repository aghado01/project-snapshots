Read [](file:///c%3A/Users/azrie/PDenv/UserGithub/PowerShellCore/ps.core.pwshspc/src/optimization/irls.cs#1-1), lines 1 to 120

Read [](file:///c%3A/Users/azrie/PDenv/UserGithub/PowerShellCore/ps.core.pwshspc/src/manifolds/RiemannianProductManifold.cs)

Read [](file:///c%3A/Users/azrie/PDenv/UserGithub/PowerShellCore/ps.core.pwshspc/src/estimators/PointEstimates.cs)

Read [](file:///c%3A/Users/azrie/PDenv/UserGithub/PowerShellCore/ps.core.pwshspc/src/manifolds/IRiemannianManifold.cs)

Read [](file:///c%3A/Users/azrie/PDenv/UserGithub/PowerShellCore/ps.core.pwshspc/src/estimators/GeometricMean.cs)

Read [](file:///c%3A/Users/azrie/PDenv/UserGithub/PowerShellCore/ps.core.pwshspc/src/estimators/ScatterAccumulator.cs)

Read [](file:///c%3A/Users/azrie/PDenv/UserGithub/PowerShellCore/ps.core.pwshspc/src/losses/IRobustLoss.cs)

Read [](file:///c%3A/Users/azrie/PDenv/UserGithub/PowerShellCore/ps.core.pwshspc/src/manifolds/EuclideanVectorManifold.cs)

Read [](file:///c%3A/Users/azrie/PDenv/UserGithub/PowerShellCore/ps.core.pwshspc/src/estimators/GeometricMedian.cs)

Let me read the rest of IRLS and check for any core Median file:

Searched for files matching `ps.core.pwshspc` · `src/**/*.cs`, 20 matches

Read [](file:///c%3A/Users/azrie/PDenv/UserGithub/PowerShellCore/ps.core.pwshspc/src/optimization/irls.cs#121-121), lines 121 to 300

Here's my review. Several issues ranging from critical to minor:

---

**Critical — would not compile or produces incorrect results**

1. **Interface fragmentation.** IRiemannianManifold.cs declares a non-generic interface with `ReadOnlySpan<double>` params. `EuclideanVectorManifold` and `Irls.Solve` both use `IRiemannianManifold<double[]>` (not defined anywhere in the attached files). `ProductManifold` implements the non-generic. The result: `ProductManifold` cannot satisfy `IRiemannianManifold<double[]>`, so it can't be passed to `Irls.Solve` or `ScatterAccumulator.Accumulate`. The entire product-manifold path is broken until the interface is resolved to one form.

2. **Type error in Distance calls.** In `Irls.Solve` step 3:

   ```csharp
   double shift = manifold.Distance(in destination, next); // next: Span<double>, not double[]
   ```

   Same in `KarcherStep`. The backing rented array `pNext`/`nextBuf` (`double[]`) should be used instead of the span alias.

3. **Subgradient sign error.** `AddScaled(subgrad, logBuf, -w)` accumulates `-Σ(wᵢ/rᵢ) log_p(xᵢ)`, which is the Riemannian gradient (points _away_ from data). Then `logBuf[d] = eta * subgrad[d]` steps _along_ the gradient — ascent, not descent. Fix: accumulate with `+w` so `subgrad` holds the descent direction `Σ(wᵢ/rᵢ) log_p(xᵢ)` (toward data). The comment "already negated above" then applies correctly.

---

**Gaps — missing files**

4. **`Median.Core.cs` absent.** GeometricMedian.cs (Median.Wrappers.cs) calls the span-based `ComputeWithScatter` core overload — no such partial class exists in the provided files. Either it was not created yet or not attached.

5. **`IrlsOptions` not defined.** The struct and its enums (`HybridMode`, `SingularityPolicy`, `ConvergenceCriterion`, `Eta0`, `SubgradientThreshold`) are referenced throughout but not provided. `Mean.cs` also passes `IrlsOptions opts = default` — needs the struct to exist.

---

**Minor**

6. **ScatterAccumulator.cs missing `using` directives.** Uses `ArrayPool<T>` (needs `using System.Buffers;`) and `IRiemannianManifold<double[]>` (needs `using Manifolds;`).

7. **PointEstimates.cs wrong namespace.** Declares `namespace StatisticalEstimators` — design doc places diagnostics under `Estimators.Diagnostics`.

8. **`Mean.cs` constraint mismatch.** `Compute` and `ComputeWithScatter` constrain `where TManifold : struct, IRiemannianManifold` (non-generic), but `Irls.Solve` inside requires `IRiemannianManifold<double[]>`. These must agree once issue #1 is resolved.
