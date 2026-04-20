# Reposnapshot copy-pasta script

```PowerShell

import-module "C:/Users/azrie/PDenv/UserGithub/PowerShellCore/ps.core.reposnapshot/reposnapshot.psm1" -Force
$shardsize = 32768
$targetDir = "ps.core.pwshspc/src"
$target = "C:/Users/azrie/PDenv/UserGithub/PowerShellCore/$targetDir"
$ignoreDirectories = @("tests", "schemas", ".snapshot", ".threadparser", ".threadparsed", ".vscode", ".depr", ".experiments", "ingest", ".schemas", ".feedback", ".legacy", ".discussion", ".notes", ".copilot", ".git-old", "assemblies", "enhancements", "tests", "preprocessing")
$ignoreFiles = @("*.md","*.txt", "*.json", "*.jsonl","*.yml","*.ini","*.gitignore","*__init__.py","*.scratch.md") #"*.md",
$ignores = $ignoreDirectories + $ignoreFiles
# SelectionOverrides @("*.py", "*.ps1")
Get-ShardedRepoSnapshot $target -MaxShardSpanBytes $shardsize -Strategy "FileLevel" -ExtraExcludePatterns $ignores -StripComments $False -IncludeFileContent $True


$target = "C:\Users\azrie\PDenv\UserGithub\project-snapshots"
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
