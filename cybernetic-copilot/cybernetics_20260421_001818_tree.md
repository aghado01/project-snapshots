# Tree Manifest TOC for Snapshot: `cybernetics_20260421_001818_s*.txt`

Strategy: FileLevel | MaxShardSpanBytes: 32768 | Created: 20260421_001819 | Shards: 4

Payload:
`./cybernetics_20260421_001818_tree.md`
`./cybernetics_20260421_001818_s001.txt` files:3
`./cybernetics_20260421_001818_s002.txt` files:1
`./cybernetics_20260421_001818_s003.txt` files:2
`./cybernetics_20260421_001818_s004.txt` files:2

## Instructions

Treat this payload like a virtual database which may be selectively scanned/seeked with the provided byte offsets available for random-access and intentional seeking/fetching.
You can manage "firehose" context overload by selectively seeking segments of the payload shard file(s) iteratively over multiple inference cycles.
The file extension of the shard files are intentionally .txt to encourage use of lower level tools like `read_file` instead of json tools.
Do not use grep to search the data because it will return an explosion of duplications.

## Tree for `cybernetics_20260421_001818_s*.txt`
```
file row metadata: name<TAB>shard_index<TAB>row_offset<TAB>row_meta_end<TAB>row_content_begin<TAB>row_content_end
cybernetics
    CopilotContextManagement.psm1	s001	276	342	346	6458
    CopilotObservation.psm1	s001	6462	6524	6528	17168
    CopilotSupervision.psm1	s001	17172	17232	17236	26296
    CyberneticAutomata.psm1	s002	138	201	205	19677
    CyberneticConsole.psm1	s003	138	200	204	27999
    CyberneticLogger.psm1	s003	28003	28061	28065	32896
    CyberneticMemorySystem.psm1	s004	138	202	206	6298
    CyberneticSupervisor.psm1	s004	6302	6366	6370	17455
```