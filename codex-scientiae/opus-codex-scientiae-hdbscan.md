Let me pull the exact `rectangle-gap` distance definition from the engine so I describe it precisely rather than from memory:

Here's how the clustering stage works end to end, grounded in the current code.

## 1. The input artifact

Figure clustering consumes exactly **one** lane from the converter: **`{slug}.paths.jsonl`** — Lane 4, the vector-path lane emitted by `ConvertTo-PdfDigIr` ([pdfdig-ir.ps1](src/pdf-converter/pdfdig-ir.ps1)). Each line is one PDF path:

```json
{
  "id": 85,
  "page": 5,
  "is_clipping": false,
  "is_filled": false,
  "is_stroked": true,
  "line_width": 0.4,
  "subpaths": 1,
  "kinds": ["line"],
  "bbox": [547.59, 537.79, 547.59, 544.27],
  "bbox_source": "api",
  "rule": null
}
```

Two other lanes are read by `ConvertTo-FigureRegions` but **not** for clustering:

- `{slug}.letters.jsonl` → only to get the modal body-font size for the em² area normalization.
- `{slug}.blocks.jsonl` → only for caption reattachment, after clustering.

Note what's _absent_: raster images (`\includegraphics` bitmaps) are XObjects, not paths, so they never enter `paths.jsonl` — that's the raster-blindness the batch exposed.

## 2. How the input is shaped

In [`ConvertTo-FigureRegions`](src/pdf-converter/pdfdig-figures.ps1) the shaping is:

1. **Load** every path that has a usable `bbox` (post-Tier-1: _all_ paths, rules included) — `pdfdig-figures.ps1:244`.
2. **Group by page** — clustering is **per-page**, independent (`:251`). A figure never spans pages, and this keeps each HDBSCAN run small.
3. Per page, each path becomes a **4-D point = its bbox corners**, written to a temp JSONL (`:264`):
   ```
   {"id":85,"v":[x0, y0, x1, y1]}     # v = [left, bottom, right, top], PDF pts, y-up
   ```
   The write order _is_ the label-index mapping used to map results back.
4. **Short-circuit:** a page with ≤ `min_pts` (3) paths skips HDBSCAN and is grouped as one tentative region flagged `too_few_to_cluster` (`:257`).

So the feature space is literally the raw bounding boxes — no centroid reduction, no normalization. Extent is preserved because the _metric_ consumes the whole box.

## 3. The metric (this is the crux)

The configured metric is **`rectangle-gap`** — [`RectangleGapMetric`](src/hdbscan/Metric.cs:250). It is _not_ a point metric; it treats each 4-D vector as an axis-aligned box `[x0,y0,x1,y1]` and returns the **nearest-point gap** between two boxes:

```
per axis i:  gap_i = max(0,  b.lo_i − a.hi_i,  a.lo_i − b.hi_i)   # 0 if the intervals overlap
distance   = √(Σ gap_i²)                                          # corner-to-corner Euclidean
```

Overlapping boxes are distance **0**. So HDBSCAN reads **density over white-space gaps**: a figure is a dense blob of low-gap paths; isolated furniture sits far away and falls out as noise (`−1`). It's a symmetric non-negative dissimilarity with `d(A,A)=0` — enough for mutual-reachability, not a true metric (fine for HDBSCAN). It's deliberately parameter-free; anisotropy (vertical line-leading ≪ horizontal gutters) would be handled _upstream_ by scaling coordinates before feeding them — but currently nothing scales them, so it's isotropic.

## 4. How HDBSCAN is configured

From the `figure_regions` block of [classify-config.json](src/pdf-converter/stores/classify-config.json), passed through the [`Invoke-Hdbscan`](src/pdf-converter/Invoke-Hdbscan.ps1) wrapper to the CLI (`pdfdig-figures.ps1:269`):

| setting                     | value                    | meaning                                                                          |
| --------------------------- | ------------------------ | -------------------------------------------------------------------------------- |
| `metric`                    | `rectangle-gap`          | the box-gap dissimilarity above                                                  |
| `min_pts`                   | **3**                    | core-distance neighborhood (density estimate)                                    |
| `min_cluster_size`          | **3**                    | smallest surviving cluster                                                       |
| `allow_single_cluster`      | **true**                 | a lone figure can be one cluster; still splits well-separated multi-figure pages |
| `cluster_selection_epsilon` | 0, except during de-frag | HDBSCAN/DBSCAN-hybrid merge distance                                             |

HDBSCAN writes two artifacts per page: `hdbscan_partition.csv` (one label per point, `−1`=noise) and `hdbscan_dendrogram.json`.

## 5. What happens after HDBSCAN

Clustering doesn't decide "figure" — that's post-processing:

- **De-fragmentation** (`:286`): if a page yields > `fragmentation_flag_min_clusters` (8) clusters, walk the dendrogram for the fragment-adjacency elbow and **re-run HDBSCAN** with that distance as `--cluster-selection-epsilon`, merging intra-figure fragments.
- **Region assembly** (`$addRegion`, `:215`): each label's paths → a union bbox → `area`, `area_em2 = area/body_font²`, `density = paths/area_em2`, then the **kind** decision:
  ```
  degenerate  if thin on either axis
  mark        elif area_em2 < 2.0            # glyph-scale
  sparse      elif density < 0.01            # big but under-inked = phantom  ← Tier-1
  figure      else
  ```
- Noise (`−1`) is counted, not emitted as a region.

```
paths.jsonl ─(per page)→ [x0,y0,x1,y1] points ─→ HDBSCAN(rectangle-gap, minPts=3, minClusterSize=3)
   → labels (−1=noise) ─→ [defrag if >8 clusters] ─→ union-bbox regions ─→ kind(degenerate|mark|sparse|figure)
   → {slug}.figures.jsonl
```

The short version: **per-page bounding boxes are the points, `rectangle-gap` is the metric, HDBSCAN groups low-gap boxes into blobs at `minPts=minClusterSize=3`, and a density gate decides which blobs are real figures.** The two structural weaknesses the batch pinned — fragmentation (gap-clustering shatters internally-gappy figures) and raster-blindness (`paths.jsonl` has no bitmaps) — both live in _this_ input+metric choice, which is exactly what the Tier-2 occupancy/RLSA reformulation would replace.
