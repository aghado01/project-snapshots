# Build-ReadmeToc.ps1
# Regenerates README.md for project-snapshots as a "Tree of Trees" TOC.
# Scans each top-level subdirectory for its most recent *_tree.md file.
# Derives snapshot ID from the most recent .snapshot/project-snapshots_*_tree.md.
# No external module dependencies.

param(
    [string]$RepoRoot = $PSScriptRoot
)

$excludeDirs = @('.git', '.snapshot', '.vscode', '.github')

# Derive snapshot ID from most recent .snapshot/project-snapshots_*_tree.md
$snapshotDir = Join-Path $RepoRoot '.snapshot'
$snapshotFile = Get-ChildItem -Path $snapshotDir -Filter 'project-snapshots_*_tree.md' -ErrorAction SilentlyContinue |
Sort-Object Name -Descending |
Select-Object -First 1

$snapshotId = if ($snapshotFile)
{
    [System.IO.Path]::GetFileNameWithoutExtension($snapshotFile.Name)
}
else
{
    "project-snapshots_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
}

# Scan top-level subdirectories
$subdirs = Get-ChildItem -Path $RepoRoot -Directory |
Where-Object { $_.Name -notin $excludeDirs } |
Sort-Object Name

$treeLines = [System.Collections.Generic.List[string]]::new()
$treeLines.Add('project-snapshots')

foreach ($dir in $subdirs)
{
    $treeMd = Get-ChildItem -Path $dir.FullName -Filter '*_tree.md' -ErrorAction SilentlyContinue |
    Sort-Object Name -Descending |
    Select-Object -First 1

    if ($treeMd)
    {
        $treeLines.Add("    $($dir.Name)")
        $treeLines.Add("        $($treeMd.Name)")
    }
}

$treeBlock = $treeLines -join "`n"

$readme = @"
# ``project-snapshots`` Tree of Trees manifest

## Instructions

Welcome to the project-snapshots repository. This README is the primary entry point for navigating to the primary entry point of each snapshot payload -- the TREE files. This file is a meta tree file.

For any subdirectory in this repo, you will find an associated snapshot payload, each with a detailed snapshot tree TOC ``*_tree.md`` file

## Meta Tree for ``$snapshotId``

``````
$treeBlock
``````
"@

$outPath = Join-Path $RepoRoot 'README.md'
Set-Content -Path $outPath -Value $readme -Encoding UTF8 -NoNewline
Write-Host "README.md updated -> $snapshotId"
