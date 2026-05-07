using System;
using System.IO;
using Viz;
using Viz.Adapters.Synthetic;
using Viz.Renderers;
using static SyntheticDatasets.SyntheticData;

// ── Generate ─────────────────────────────────────────────────────────────────
var dataset = GenerateCrescentAndEllipsoid();

// ── Adapt ─────────────────────────────────────────────────────────────────────
var adapter = new SyntheticDatasetAdapter();
var vizDataset = adapter.Adapt(dataset);

// ── Build scene ───────────────────────────────────────────────────────────────
var descriptor = new SceneDescriptor
{
    Title = "Crescent + Ellipsoid (smoke test)",
    Hints = SceneRenderHints.Default,
};
var package = SceneBuilder.Build(vizDataset, descriptor);

// ── Render to disk ────────────────────────────────────────────────────────────
string outPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    "viz-smoke.html");

using (var stream = File.Create(outPath))
    new ThreeJsHtmlRenderTarget().Render(package, stream);

Console.WriteLine($"Written: {outPath}");
Console.WriteLine("Open in browser to inspect.");
