## 1.2.1

### Fixed
* Discovery against appliances with a large number of application domains
  (200+) where the orchestrator's API user lacks read access to many of them
  no longer produces a SQL truncation failure in Keyfactor Command. Two
  related fixes:
  * `FlowLogger.GetSummary()` output is now capped at 3500 characters with
    a `[truncated, N chars omitted; check orchestrator log for full
    breadcrumb]` marker. Defense-in-depth against Command's
    `AgentJobStatus.Message` column overflow (NVARCHAR(4000)).
  * Discovery aggregates per-domain failures by error signature instead of
    emitting one `FAIL` and one `SKIP` line per failed domain. Identical
    HTTP errors across N domains collapse into one summary line with a
    count and a 5-domain sample. Successful domains still emit one
    `Discovered-<path>` step each.

  Net effect on a 235-domain appliance with 114 inaccessible domains: the
  breadcrumb summary drops from ~50 KB and 235+ step lines to ~1 KB and
  ~10 step lines, well under Command's column cap.

## 1.2.0

### Added
* Discovery job: automatically enumerates all application domains on a DataPower
  appliance via `GET /mgmt/domains/config/`, then queries each domain's filestore
  via `GET /mgmt/filestore/{domain}` to surface certificate store directories
  (`cert`, `pubcert`, `sharedcert`). Eliminates manual creation of certificate
  stores per domain in environments with many domains.
* `FlowLogger` step-oriented breadcrumb logger; every job (Inventory, Management,
  Discovery) wraps its `ProcessJob` body in a `using (var flow = new FlowLogger(...))`
  block. The step summary is appended to `JobResult.FailureMessage` on both success
  and failure so operators can scan job-history without enabling trace logs.
* `DataPowerApiException`: typed API error carrying HTTP status code and trimmed
  response body. `Find()` walker unwraps `AggregateException` chains so
  `JobBase.DescribeException` can surface the underlying HTTP detail to operators.
* `JobBase`: shared plumbing for PAM resolution (warn-on-empty fallback), JobResult
  helpers that auto-append flow summaries, and exception unwrapping.
* Spec documentation for the Discovery feature: `docs/discovery-overview.md`,
  `docs/discovery-overview.pdf`, and the original HTML version.

### Changed
* `DataPowerClient.ApiRequestString` now throws the typed `DataPowerApiException`
  on `WebException`, capturing the HTTP status and response body from the failed
  response. Previously errors were re-thrown as raw `WebException`.
* Trace logging masks sensitive payload fields (`content`, `Password`,
  `PasswordAlias`) before serializing the request body to the log.
* Request and response streams are now wrapped in `using` blocks for deterministic
  disposal.
* `Inventory`, `Management`, and `Discovery` jobs all derive from `JobBase` and
  perform null-argument validation at the public boundary before any work begins.

### Documentation
* `docsource/content.md` and `readme_source.md` now have a dedicated "Store Path
  Format" section explaining `<domain>\<directory>`, the three certificate
  directory types (`cert`, `pubcert`, `sharedcert`), and their scoping.
* Discovery section added to both source docs.
* `.gitignore` updated to exclude `.claude/` (per-machine IDE state),
  `.secrets/`, and `*.env` files.

## 1.1.2

* Added Support for new version of Data Power and Backwards for Old Versions After Data Power API Breaking Changes
  
## 1.1.1

* Dual Build .Net 6 and .Net 8 support
* Test Tool Modifications
* Readme Updates

## 1.1.0

* Convert to Universal Orchestrator Framework
* Added Support for .cer files during inventory
* Added PAM Support

## 1.0.0

* Windows Orchestrator with Add, Remove and Inventory Capabilities
