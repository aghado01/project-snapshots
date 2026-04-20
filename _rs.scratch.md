# Reposnapshot copy-pasta script

```PowerShell

import-module "C:/Users/azrie/PDenv/UserGithub/PowerShellCore/ps.core.reposnapshot/reposnapshot.psm1" -Force
$shardsize = 32768
# $targetDir = "pet-projects/prompt-harness"
# $target = "C:/Users/azrie/PDenv/UserGithub/PowerShellCore/$targetDir"
$target = "C:\Users\azrie\PDenv\UserGithub\project-snapshots"
$ignoreDirectories = @("tests", "schemas", ".snapshot", ".threadparser", ".threadparsed", ".vscode", ".depr", ".experiments", "ingest", ".schemas", ".feedback", ".legacy", ".discussion", ".notes", ".copilot", ".git-old", "assemblies", "enhancements", "tests", "preprocessing")
$ignoreFiles = @("*.txt", "*.json", "*.jsonl","*.yml","*.ini","*.gitignore","__init__.py") #"*.md",
$ignores = $ignoreDirectories + $ignoreFiles
# SelectionOverrides @("*.py", "*.ps1")
Get-ShardedRepoSnapshot $target -MaxShardSpanBytes $shardsize -Strategy "FileLevel" -ExtraExcludePatterns $ignores -StripComments $True -IncludeFileContent $False

```

Github connector, repository `PowerShellCore`:

`PowerShellCore/ps.core.pwshspc/src/.snapshot/src_20260416_133632_tree.md`
`PowerShellCore/ps.core.pwshspc/src/.snapshot/src_20260416_133632_s001.txt`

`PowerShellCore/ps.core.pwshspc/src/.notes/spc.batch.patch-notes.md`
`PowerShellCore/ps.core.pwshspc/.discussion/mini-flourish`

`PowerShellCore/ps.core.pwshspc/src/**`
`PowerShellCore/ps.core.pwshspc/src/.notes`
