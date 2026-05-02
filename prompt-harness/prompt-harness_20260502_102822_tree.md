# Tree Manifest TOC for Snapshot: `prompt-harness_20260502_102822_s*.txt`

Strategy: FileLevel | MaxShardSpanBytes: 32768 | Created: 20260502_102823 | Shards: 5

Payload:
`./prompt-harness_20260502_102822_tree.md`
`./prompt-harness_20260502_102822_s001.txt` files:19
`./prompt-harness_20260502_102822_s002.txt` files:15
`./prompt-harness_20260502_102822_s003.txt` files:4
`./prompt-harness_20260502_102822_s004.txt` files:5
`./prompt-harness_20260502_102822_s005.txt` files:9

## Instructions

Treat this payload like a virtual database which may be selectively scanned/seeked with the provided byte offsets available for random-access and intentional seeking/fetching.
You can manage "firehose" context overload by selectively seeking segments of the payload shard file(s) iteratively over multiple inference cycles.
The file extension of the shard files are intentionally .txt to encourage use of lower level tools like `read_file` instead of json tools.
Do not use grep to search the data because it will return an explosion of duplications.

## Tree for `prompt-harness_20260502_102822_s*.txt`
```
file row metadata: name<TAB>shard_index<TAB>row_offset<TAB>row_meta_end<TAB>row_content_begin<TAB>row_content_end
prompt-harness
    .pytest_cache
        v
            cache
                lastfailed	s001	399	460	464	465
                nodeids	s001	469	534	538	4919
        CACHEDIR.TAG	s001	138	198	202	395
    eval
        __init__.py	s001	4923	4973	4977	5140
        suite.py	s001	5144	5194	5198	8601
    scripts
        _repo-init.ps1	s001	9132	9191	9195	13011
        background_run_worker.ps1	s001	13015	13085	13089	15595
        compare_gates.ps1	s001	15599	15658	15662	16135
        compare_to_baseline.ps1	s001	16139	16204	16208	16341
        ensure_venv.ps1	s001	16345	16406	16410	21032
        generate_probe_payloads.ps1	s001	21036	21107	21111	22110
        get_background_run.ps1	s001	22114	22182	22186	24415
        install_deps.ps1	s001	24419	24478	24482	24676
        python.ps1	s001	24680	24733	24737	25186
        refresh_model_inventory.ps1	s001	25190	25263	25267	27589
        resolve_python.ps1	s001	27593	27657	27661	30563
        run_baseline.ps1	s001	30567	30626	30630	31022
        run_tests.ps1	s001	31026	31082	31086	31359
        start_background_run.ps1	s002	138	208	212	3639
        update_baseline_alias.ps1	s002	3643	3712	3716	4668
    src
        client
            __init__.py	s002	4724	4776	4780	4780
            providers.py	s002	4783	4844	4848	13678
        metrics
            __init__.py	s002	13682	13735	13739	13739
            agreement.py	s002	13742	13804	13808	15072
            calibration.py	s002	15076	15140	15144	17466
            citations.py	s002	17470	17532	17536	20698
            consistency.py	s002	20702	20766	20770	23809
            grounding.py	s002	23813	23875	23879	27080
            mercury.py	s002	27084	27141	27145	27468
            significance.py	s002	27472	27537	27541	30396
        probes
            __init__.py	s002	30400	30457	30461	31172
            adapters.py	s002	31176	31236	31240	32845
            cli.py	s003	138	193	197	8326
            payloads.py	s003	8330	8390	8394	14948
            unicode.py	s003	14952	15011	15015	22415
        render
            __init__.py	s003	22419	22479	22483	24737
            engine.py	s004	138	198	202	11448
            examples.py	s004	11452	11512	11516	20176
            report.py	s004	20180	20238	20242	26541
            slice_delta.py	s004	26545	26608	26612	28453
        rubrics
            __init__.py	s004	28457	28510	28514	28514
            engine.py	s005	138	197	201	8210
        runners
            __init__.py	s005	8214	8267	8271	8271
            batch.py	s005	8274	8335	8339	23491
            compare.py	s005	23495	23555	23559	28975
        slices
            __init__.py	s005	28979	29031	29035	29035
            label.py	s005	29038	29095	29099	31450
        __init__.py	s002	4672	4717	4721	4721
    templates
        probes
            simple-integer.j2	s005	31454	31523	31527	31742
            simple-whitespace.j2	s005	31746	31818	31822	32044
        table_specs
            slice_summary.py	s005	32048	32121	32125	32872
    prompt-harness.code-workspace	s001	8605	8668	8672	9128
```