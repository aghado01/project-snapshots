# Tree Manifest TOC for Snapshot: `bayesian-driver_20260420_234531_s*.txt`

Strategy: FileLevel | MaxShardSpanBytes: 32768 | Created: 20260420_234532 | Shards: 2

Payload:
`./bayesian-driver_20260420_234531_tree.md`
`./bayesian-driver_20260420_234531_s001.txt` files:8
`./bayesian-driver_20260420_234531_s002.txt` files:6

## Instructions

Treat this payload like a virtual database which may be selectively scanned/seeked with the provided byte offsets available for random-access and intentional seeking/fetching.
You can manage "firehose" context overload by selectively seeking segments of the payload shard file(s) iteratively over multiple inference cycles.
The file extension of the shard files are intentionally .txt to encourage use of lower level tools like `read_file` instead of json tools.
Do not use grep to search the data because it will return an explosion of duplications.

## Tree for `bayesian-driver_20260420_234531_s*.txt`
```
file row metadata: name<TAB>shard_index<TAB>row_offset<TAB>row_meta_end<TAB>row_content_begin<TAB>row_content_end
bayesian-driver
    environment
        hmm_volatility_controller.py	s001	2853	2930	2934	4120
        scene_renderer.py	s001	4124	4190	4194	8808
        time_varying_bias_environment.py	s001	8812	8890	8894	9696
        volatility_controller.py	s001	9700	9770	9774	10755
    models
        pretrained_relu_generator.py	s001	10759	10823	10827	10827
        recursive_bayesian_learner.py	s001	10830	10906	10910	29079
        simple_relu_generator.py	s001	29083	29151	29155	32340
        surprise_detector.py	s002	138	202	206	1809
        utils_particle.py	s002	1813	1871	1875	2024
    utils
        compute_optimizations.py	s002	2028	2096	2100	9616
        demo.py	s002	9620	9668	9672	10549
        device.py	s002	10553	10606	10610	13430
        visualization.py	s002	13434	13491	13495	13669
    config.py	s001	276	322	326	2849
```
