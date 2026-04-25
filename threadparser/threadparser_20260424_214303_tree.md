# Tree Manifest TOC for Snapshot: `threadparser_20260424_214303_s*.txt`

Strategy: FileLevel | MaxShardSpanBytes: 32768 | Created: 20260424_214305 | Shards: 7

Payload:
`./threadparser_20260424_214303_tree.md`
`./threadparser_20260424_214303_s001.txt` files:1
`./threadparser_20260424_214303_s002.txt` files:1
`./threadparser_20260424_214303_s003.txt` files:1
`./threadparser_20260424_214303_s004.txt` files:2
`./threadparser_20260424_214303_s005.txt` files:1
`./threadparser_20260424_214303_s006.txt` files:11
`./threadparser_20260424_214303_s007.txt` files:6

## Instructions

Treat this payload like a virtual database which may be selectively scanned/seeked with the provided byte offsets available for random-access and intentional seeking/fetching.
You can manage "firehose" context overload by selectively seeking segments of the payload shard file(s) iteratively over multiple inference cycles.
The file extension of the shard files are intentionally .txt to encourage use of lower level tools like `read_file` instead of json tools.
Do not use grep to search the data because it will return an explosion of duplications.

## Tree for `threadparser_20260424_214303_s*.txt`
```
file row metadata: name<TAB>shard_index<TAB>row_offset<TAB>row_meta_end<TAB>row_content_begin<TAB>row_content_end
threadparser
    streaming-segmentation
        streaming-segmentation-v1.psm1	s001	138	231	235	22644
    v1
        threadparser.psm1	s003	138	198	202	45890
    v1-batch
        threadparser-batch.psm1	s002	138	210	214	15953
    v2
        threadparser_v2-primitives.psm1	s004	778	849	853	7380
        threadparser-v2.psm1	s005	138	201	205	46032
    v2-new
        tp.core.psm1	s004	138	191	195	774
    v3
        test-threadparser-v3-visual.ps1	s006	138	206	210	639
        test-threadparser-v3.ps1	s006	643	704	708	977
        threadparser-v3.psm1	s006	981	1041	1045	10872
    v4
        normalize-whitespace.psm1	s006	10876	10941	10945	12541
        threadparser-v4-draft-1.ps1	s006	12545	12613	12617	16547
        threadparser-v4-draft-result	s006	16551	16620	16624	19976
        threadparser-v4-draft-result-copilot-vscode-example	s006	19980	20072	20076	21304
    vCopilotCloud
        ontology.psd1	s006	21308	21373	21377	22785
        v1_Get-SegmentsFromSemanticEvents.psm1	s006	22789	22879	22883	26364
        v1_Invoke-BatchProfileSegmentation.psm1	s006	26368	26459	26463	30715
        v1_Invoke-ProfileSegmentation.psm1	s006	30719	30805	30809	32741
        v1_ontology.psd1	s007	138	206	210	1618
        v1_Resolve-SemanticBoundaries.psm1	s007	1622	1708	1712	5421
        v1_threadparser-profile-segmenters.psm1	s007	5425	5516	5520	9792
        v1_threadparser-vnext-code-region-detector-contextaware.psm1	s007	9796	9908	9912	13720
        v1_threadparser-vnext-code-region-detector.psm1	s007	13724	13823	13827	16973
        v1_threadparser-vnext-segmentation-guards.psm1	s007	16977	17075	17079	20881
```
