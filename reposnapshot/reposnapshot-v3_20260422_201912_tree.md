# Tree Manifest TOC for Snapshot: `reposnapshot-v3_20260422_201912_s*.txt`

Strategy: FileLevel | MaxShardSpanBytes: 32768 | Created: 20260422_201913 | Shards: 4

Payload:
`./reposnapshot-v3_20260422_201912_tree.md`
`./reposnapshot-v3_20260422_201912_s001.txt` files:6
`./reposnapshot-v3_20260422_201912_s002.txt` files:2
`./reposnapshot-v3_20260422_201912_s003.txt` files:2
`./reposnapshot-v3_20260422_201912_s004.txt` files:1

## Instructions

Treat this payload like a virtual database which may be selectively scanned/seeked with the provided byte offsets available for random-access and intentional seeking/fetching.
You can manage "firehose" context overload by selectively seeking segments of the payload shard file(s) iteratively over multiple inference cycles.
The file extension of the shard files are intentionally .txt to encourage use of lower level tools like `read_file` instead of json tools.
Do not use grep to search the data because it will return an explosion of duplications.

## Tree for `reposnapshot-v3_20260422_201912_s*.txt`
```
file row metadata: name<TAB>shard_index<TAB>row_offset<TAB>row_meta_end<TAB>row_content_begin<TAB>row_content_end
reposnapshot-v3
    processors
        chain-executor.ps1	s001	276	339	343	911
        file-read.ps1	s001	915	974	978	1988
        format-ws.ps1	s001	1992	2053	2057	4077
        rs-csstrip.ps1	s001	4081	4143	4147	10626
        rs-indent.ps1	s001	10630	10691	10695	15627
        rs-psstrip.ps1	s001	15631	15696	15700	27160
    rs.core.colonel.v2.psm1	s002	138	201	205	16307
    rs.core.crawler.psm1	s002	16311	16368	16372	23357
    rs.core.ignore.psm1	s003	138	197	201	28098
    rs.core.ingest.psm1	s003	28102	28158	28162	31731
    rs.core.internals.psm1	s004	138	198	202	3935
```
