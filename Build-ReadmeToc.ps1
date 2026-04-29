# Build-ReadmeToc.ps1
# Regenerates README.md for project-snapshots as a "Tree of Trees" TOC.
# Scans each top-level subdirectory for its most recent *_tree.md file.
# Derives snapshot ID from the most recent .snapshot/project-snapshots_*_tree.md.
# No external module dependencies.

param(
    [string]$RepoRoot = $PSScriptRoot
)

$excludeDirs = @('.git', '.snapshot', '.vscode', '.github')

# Generate a fresh timestamp for this README rebuild
$snapshotId = Get-Date -Format 'yyyyMMdd_HHmmss'

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

        # Supplementary: files that are not the tree .md and not payload shards (*_s*.txt)
        $supplementary = Get-ChildItem -Path $dir.FullName -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ne $treeMd.Name -and $_.Name -notmatch '_s\d+\.txt$' } |
        Sort-Object Name

        # Append/replace Supplementary files section in the tree.md itself
        $treeMdPath = $treeMd.FullName
        $existingContent = Get-Content -Path $treeMdPath -Raw -Encoding UTF8

        # Strip any prior supplementary section (from the marker onward)
        $marker = "`nSupplementary files:"
        $markerIdx = $existingContent.IndexOf($marker)
        $baseContent = if ($markerIdx -ge 0) { $existingContent.Substring(0, $markerIdx) } else { $existingContent.TrimEnd() }

        if ($supplementary)
        {
            $suppLines = [System.Collections.Generic.List[string]]::new()
            $suppLines.Add('')
            $suppLines.Add('Supplementary files:')
            foreach ($f in $supplementary)
            {
                $suppLines.Add($f.Name)
            }
            $newTreeContent = $baseContent + ($suppLines -join "`n")
        }
        else
        {
            $newTreeContent = $baseContent
        }

        Set-Content -Path $treeMdPath -Value $newTreeContent -Encoding UTF8 -NoNewline
    }
}

$treeBlock = $treeLines -join "`n"

$readme = @"
# ``project-snapshots`` Tree of Trees manifest

## Instructions

Welcome to the project-snapshots repository. This README is the primary entry point for navigating to the primary entry point of each snapshot payload -- the ``_tree.md`` files seen in each project snapshot subdirectory.

Each \*\_tree.md file contains byte-offset indexed file metadata for selective LLM context loading from the corresponding ``*.txt`` sharded snapshot payload files.

## Meta Tree ``$snapshotId``

``````
$treeBlock
``````
"@

$outPath = Join-Path $RepoRoot 'README.md'
Set-Content -Path $outPath -Value $readme -Encoding UTF8 -NoNewline
Write-Host "README.md updated -> $snapshotId"
