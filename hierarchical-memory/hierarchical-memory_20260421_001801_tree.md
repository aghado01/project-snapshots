# Tree Manifest TOC for Snapshot: `hierarchical-memory_20260421_001801_s*.txt`

Strategy: FileLevel | MaxShardSpanBytes: 32768 | Created: 20260421_001802 | Shards: 1

Payload:
`./hierarchical-memory_20260421_001801_tree.md`
`./hierarchical-memory_20260421_001801_s001.txt` files:2

## Instructions

Treat this payload like a virtual database which may be selectively scanned/seeked with the provided byte offsets available for random-access and intentional seeking/fetching.
You can manage "firehose" context overload by selectively seeking segments of the payload shard file(s) iteratively over multiple inference cycles.
The file extension of the shard files are intentionally .txt to encourage use of lower level tools like `read_file` instead of json tools.
Do not use grep to search the data because it will return an explosion of duplications.

## Tree for `hierarchical-memory_20260421_001801_s*.txt`
```
file row metadata: name<TAB>shard_index<TAB>row_offset<TAB>row_meta_end<TAB>row_content_begin<TAB>row_content_end
hierarchical-memory
    MemorySystem.ps1	s001	276	332	336	11038
    MemorySystem.test.ps1	s001	11042	11100	11104	14480
```
