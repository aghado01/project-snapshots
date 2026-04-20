# Invoke-PublishSnapshot.ps1
# Rebuilds README.md and commits + pushes to remote.

param(
    [string]$RepoRoot = $PSScriptRoot,
    [string]$Message = "chore: rebuild README TOC"
)

Push-Location $RepoRoot

try
{
    Write-Host "==> Pulling latest from remote..."
    git pull --ff-only
    if ($LASTEXITCODE -ne 0) { throw "git pull failed — resolve conflicts before publishing." }

    Write-Host "==> Rebuilding README..."
    & "$RepoRoot\Build-ReadmeToc.ps1" -RepoRoot $RepoRoot

    $diff = git diff --name-only README.md
    if (-not $diff)
    {
        Write-Host "README.md unchanged, nothing to commit."
        return
    }

    Write-Host "==> Staging and committing..."
    git add README.md
    git commit -m $Message

    Write-Host "==> Pushing..."
    git push

    Write-Host "Done."
}
finally
{
    Pop-Location
}
