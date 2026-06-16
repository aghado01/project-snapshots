# Tree Manifest TOC for Snapshot: `CyberneticCodePilot_20260608_204032_s*.txt`

Strategy: FixedSize | Grouping: ByRootDirectory | Packing: Balanced | MaxShardSpanBytes: 32768 | Created: 20260608_204033 | Shards: 4

Payload:
`./CyberneticCodePilot_20260608_204032_tree.md`
`./CyberneticCodePilot_20260608_204032_s001_bootstrap.txt` files:2
`./CyberneticCodePilot_20260608_204032_s002_primitives.txt` files:1
`./CyberneticCodePilot_20260608_204032_s003_primitives.txt` files:1
`./CyberneticCodePilot_20260608_204032_s004_toolbelt.txt` files:1

## Instructions

Treat this payload like a virtual database which may be selectively scanned/seeked with byte offsets available for random-access and intentional seeking/fetching.
You can manage "firehose" context overload by selectively seeking segments of the payload file iteratively over multiple inference cycles.
Do not use grep to search the data because it will return an explosion of duplications.
Seek to `row_offset` in the .json file to read any entry directly without scanning.
The shard files are intentionally .txt to encourage use of lower level tools like `read_file` instead of json tools.

## Tree for `CyberneticCodePilot_20260608_204032_s*.txt`
```
file row metadata: name<TAB>shard_index<TAB>row_offset<TAB>row_meta_end<TAB>row_content_begin<TAB>row_content_end
CyberneticCodePilot
    bootstrap
        shims
            launch-claude.bat	s001	3265	3332	3336	4258
        launch-generic.bat	s001	138	203	207	3263
    primitives
        jso-hash.ps1	s002	138	198	202	2847
        jso-jackson.ps1	s003	138	204	208	58003
    toolbelt
        jso-debug.ps1	s004	138	200	204	33898
```