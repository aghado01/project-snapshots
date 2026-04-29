# Tree Manifest TOC for Snapshot: `prompt-harness_20260420_010807_s*.txt`

Strategy: FileLevel | MaxShardSpanBytes: 32768 | Created: 20260420_010810 | Shards: 2

Payload:
`./prompt-harness_20260420_010807_tree.md`
`./prompt-harness_20260420_010807_s001.txt` files:19
`./prompt-harness_20260420_010807_s002.txt` files:11

## Instructions

Treat this payload like a virtual database which may be selectively scanned/seeked with the provided byte offsets available for random-access and intentional seeking/fetching.
You can manage "firehose" context overload by selectively seeking segments of the payload shard file(s) iteratively over multiple inference cycles.
The file extension of the shard files are intentionally .txt to encourage use of lower level tools like `read_file` instead of json tools.
Do not use grep to search the data because it will return an explosion of duplications.

NOTE: There are supplemental materials included in this project's snapshot directory which are not included in the payload files, namely configuration and documentation files. See aditional files in this directory that are not covered by the snapshot:

for `data/prompts`, see:
`./ambiguity.jsonl`
`./citations.jsonl`
`./instructions.jsonl`

for `github/workflows`, see:
`./ci.yml`

also see `./WALKTHROUGH.md`, `./README.md`, `requirements.txt`

## Tree for `prompt-harness_20260420_010807_s*.txt`

```
file row metadata: name<TAB>shard_index<TAB>row_offset<TAB>row_meta_end<TAB>row_content_begin<TAB>row_content_end
prompt-harness
    configs
        gates_mercury.yaml	s001	276	336	340	699
        gates.yaml	s001	703	755	759	1426
        providers.yaml	s001	1430	1486	1490	1652
        rubrics.yaml	s001	1656	1710	1714	2326
    eval
        __init__.py	s001	2330	2375	2379	2379
    scripts
        compare_gates.ps1	s001	5983	6042	6046	6484
        compare_to_baseline.ps1	s001	6488	6550	6554	6633
        run_baseline.ps1	s001	6637	6695	6699	7057
        update_baseline_alias.ps1	s001	7061	7123	7127	7127
    src
        client
            __init__.py	s001	7182	7234	7238	7238
            providers.py	s001	7241	7302	7306	11404
        metrics
            __init__.py	s001	11408	11461	11465	11465
            citations.py	s001	11468	11530	11534	13558
            mercury.py	s001	13562	13614	13618	13618
        render
            templates
                reports
                    report.md.j2	s002	7366	7444	7448	8465
            __init__.py	s001	13621	13680	13684	15025
            engine.py	s001	15029	15087	15091	20964
            examples.py	s001	20968	21028	21032	27615
            report.py	s002	138	196	200	5525
            slice_delta.py	s002	5529	5592	5596	7362
        rubrics
            __init__.py	s002	8469	8522	8526	8526
            engine.py	s002	8529	8588	8592	10230
        runners
            __init__.py	s002	10234	10287	10291	10291
            batch.py	s002	10294	10352	10356	17905
            compare.py	s002	17909	17969	17973	22541
        slices
            __init__.py	s002	22545	22597	22601	22601
            label.py	s002	22604	22661	22665	23780
        __init__.py	s001	7130	7175	7179	7179
    templates
        table_specs
            slice_summary.py	s002	23784	23857	23861	24081
    init.ps1	s001	2382	2427	2431	5979
```
Supplementary files:
ambiguity.jsonl
ci.yml
citations.jsonl
instructions.jsonl
README.md
requirements.txt
WALKTHROUGH.md