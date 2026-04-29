# Tree Manifest TOC for Snapshot: `claudeCodeTools_20260429_084002_s*.txt`

Strategy: FileLevel | MaxShardSpanBytes: 32768 | Created: 20260429_084004 | Shards: 5

Payload:
`./claudeCodeTools_20260429_084002_tree.md`
`./claudeCodeTools_20260429_084002_s001.txt` files:1
`./claudeCodeTools_20260429_084002_s002.txt` files:2
`./claudeCodeTools_20260429_084002_s003.txt` files:1
`./claudeCodeTools_20260429_084002_s004.txt` files:2
`./claudeCodeTools_20260429_084002_s005.txt` files:1

## Instructions

Treat this payload like a virtual database which may be selectively scanned/seeked with the provided byte offsets available for random-access and intentional seeking/fetching.
You can manage "firehose" context overload by selectively seeking segments of the payload shard file(s) iteratively over multiple inference cycles.
The file extension of the shard files are intentionally .txt to encourage use of lower level tools like `read_file` instead of json tools.
Do not use grep to search the data because it will return an explosion of duplications.

## Tree for `claudeCodeTools_20260429_084002_s*.txt`
```
file row metadata: name<TAB>shard_index<TAB>row_offset<TAB>row_meta_end<TAB>row_content_begin<TAB>row_content_end
claudeCodeTools
    claude-jso-jackson.ps1	s001	138	200	204	36455
    claude-jso-markdown-v2.ps1	s002	138	204	208	17165
    claude-jso-run.ps1	s002	17169	17224	17228	27219
    claude-jso-units.ps1	s003	138	198	202	18053
    jso-debug.ps1	s004	138	191	195	29576
    jso-hash.ps1	s004	29580	29629	29633	32278
    jso-jackson.ps1	s005	138	193	197	54676
```
Supplementary files:
README.md
rpc-followup.md
SKILL.md