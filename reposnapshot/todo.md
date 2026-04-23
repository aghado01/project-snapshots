1. finish crawler/ignore decoupling

2. finish colonel.v2 chaining enhancement updates

- test full chained configs from file-read to domain specifics
- it should be possible to do preprocessing/postprocessing in the same call
- need to also handle the no-op case where files bieng processed aren't assigned to a profile
- these should pass through via default chain which does nothing, and this shoudl be indicated in diagnostics
- shore up comment stripping
- add processor for other things to strip such as IDE boilerplate, LICENSE

3. excavate `reposnapshot.psm1` LTS for shardfile export handling logic, TOC-tree formatting

- TOC files will be written differently than how its done in LTS
- likely using templating approach via scriban with Liquid compatibility mode
- eventually a custom signed scriban build with RS branding
- determine any changes to the reposnapshot.psm1 patterns that should be updated for v3 sensibilities
- can still use `.j2` files with caveats about interop exceptions like `upper`
- can probably programmatically scrub scriban-oriented j2 files in the repo via CICD/github actions

- for example we will want
  also need to harvest any comment stripping logic not covered yet in colonel processors such as IDE boilerplate, license/trademark/etc noise that may be in reposnapshot.psm1

4. build pipeline test harness

- create sufficient synthetic data test corpus for harness to check various pipeline stages
- modularity
- end to end test to check that things run correctly under nominal conditions before unit tests for failure modes
- first lay out test cases for each pipeline phase/member + nested tests within processors for example (e.g. comment stripping unit tests)

5. write admiral-tp pipeline
   implement json config after admiral is online

- this will be a bit complicated
- need design pass regarding what should be in the config files vs what remains pure bound params
- need to establish precedence with runtime bound params

6. add threadparser admiral-tp pipeline members/components + tp-specific colonel processors

incorporate pagination with sliding shannon entropy for segmentation or somesuch

7. review feature requests from rs.core notes spread across different files
   incorporate config files sidecar into reposnapshot

- markdown file with any included config file types (json, jsonl, yaml, yml based on glob selection or not-ignored inclusion)
- each file gets a markdown header section with metadata and a typed codeblock for the format containing the contents
- will need to
