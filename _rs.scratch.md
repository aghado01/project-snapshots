# Reposnapshot copy-pasta script

```PowerShell
#Ignore workflow
# import-module "C:/Users/azrie/PDenv/UserGithub/PowerShellCore/ps.core.reposnapshot/reposnapshot.psm1" -Force
import-module "C:/Users/azrie/PDenv/UserGithub/PowerShellCore/ps.core.reposnapshot/reposnapshotlts.psm1" -Force

$projectname = "spcx"
$target = "C:\Users\azrie\PDenv\UserGithub\PowerShellCore\ps.core.pwshspc\src" # "D:\pdenv\CyberneticCodePilot"
$target = $target -Replace '\\', '/'
$shardsize = 32768
$groupingStrategy = "ByRootDirectory"
$packingStrategy = "Balanced"
$includeFileContent = $True
$stripComments = $True
$excludeShardMetadata = $False
$ignoreDirectories = @("tests", "schemas", ".snapshot", ".threadparser", ".threadparsed", ".vscode", ".depr", ".experiments", "ingest", ".schemas", ".feedback", ".legacy", ".discussion", ".notes", ".copilot", ".git-old", "assemblies", "enhancements", "tests","venv", ".claude","**.cache**","smoke-test", "configs",".venv","data","docs","reports")
$ignoreFiles = @("*.md","*.txt", "*.json", "*.jsonl","*.yml","*.ini","*.csproj","*.gitignore","*.copilotignore", "*.pyc","*.scratch.md", "*snapignore.txt")
$ignores = $ignoreDirectories + $ignoreFiles
# $shardOutputDir = Join-Path $target ".snapshot"
$shardOutputDir = "C:/Users/azrie/PDenv/UserGithub/project-snapshots/$projectname"
$shardOutputDir = $shardOutputDir -Replace '\\', '/'
Get-ShardedRepoSnapshot $target -MaxShardSpanBytes $shardsize -GroupingStrategy $groupingStrategy -PackingStrategy $packingStrategy -ExtraExcludePatterns $ignores -StripComments $stripComments -IncludeFileContent $includeFileContent -ShardOutputDirectory $shardOutputDir -ExcludeShardMetadata $False



# Selections workflow
# import-module "C:/Users/azrie/PDenv/UserGithub/PowerShellCore/ps.core.reposnapshot/reposnapshot.psm1" -Force
# $target = "C:/Users/azrie/PDenv/UserGithub/PowerShellCore/pet-projects/context-guardian/src"
# $shardsize = 32768
# $selectionOverrides = @("*.py", "*.psm1","*.ps1", "*.ts")
# $includeFileContent = $True
# $stripComments = $False
# Get-ShardedRepoSnapshot $target -MaxShardSpanBytes $shardsize -Strategy "FileLevel" -StripComments $stripComments -IncludeFileContent $includeFileContent -SelectionOverrides $selectionOverrides


$target = "C:/Users/azrie/PDenv/UserGithub/project-snapshots"
$ignores = @("*.txt", "*.json", "*.gitignore","*.scratch.md")
Get-ShardedRepoSnapshot $target -MaxShardSpanBytes $shardsize -Strategy "FileLevel" -ExtraExcludePatterns $ignores -StripComments $True -IncludeFileContent $False
```

Github connector, repository `PowerShellCore`:

`PowerShellCore/ps.core.pwshspc/src/.snapshot/src_20260416_133632_tree.md`
`PowerShellCore/ps.core.pwshspc/src/.snapshot/src_20260416_133632_s001.txt`

`PowerShellCore/ps.core.pwshspc/src/.notes/spc.batch.patch-notes.md`
`PowerShellCore/ps.core.pwshspc/.discussion/mini-flourish`

`PowerShellCore/ps.core.pwshspc/src/**`
`PowerShellCore/ps.core.pwshspc/src/.notes`
