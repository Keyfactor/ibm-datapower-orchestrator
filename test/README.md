# DataPower Test Setup

Tools to populate a DataPower test appliance with domains and certificates so you can validate the Discovery and Inventory jobs in the IBM DataPower Orchestrator.

## What it creates

| Resource | Count | Location |
|----------|-------|----------|
| Application domains | 10 | `test-domain-01` through `test-domain-10` |
| Certs in `default/pubcert` | 10 | appliance-wide, visible from every domain (filestore PEMs only) |
| Cert + key files in `default/sharedcert` | 20 | 10 cert + 10 key PEMs |
| `default` CryptoCertificate / CryptoKey objects (sharedcert) | 10 + 10 | each pointing at a `sharedcert:///` PEM, living in the `default` domain |
| Cert + key files in `{domain}/cert` | 200 | 10 cert + 10 key files per domain (10 domains x 20 files) |
| Per-domain CryptoCertificate config objects | 100 | 10 per domain, each pointing at one cert PEM in `cert:///` |
| Per-domain CryptoKey config objects | 100 | 10 per domain, each pointing at one key PEM in `cert:///` |

Every cert and key uploaded is a **unique** self-signed pair, so Inventory results will show distinct thumbprints (no duplicates).

> **Why config objects matter:** the orchestrator's Inventory enumerates `CryptoCertificate` config objects (`/mgmt/config/{domain}/CryptoCertificate`), *not* the filestore, for both `cert` and `sharedcert`. A PEM sitting in `cert:///` or `sharedcert:///` without a matching CryptoCertificate object is invisible to Inventory. Folders 4 and 5 create both file and object for every cert. Pubcert is the one exception - Command reads it straight from the filestore, no config object needed.

> **sharedcert is per-domain, like cert.** Its underlying filestore is appliance-wide (owned by `default`), but the CryptoCertificate/CryptoKey objects that reference a `sharedcert://` file are scoped to whichever domain they were created in - same as `cert`. Discovery reflects that: it emits `{domain}\sharedcert` only for domains that actually own a CryptoCertificate object referencing `sharedcert://`, not for every domain that can merely *read* the filestore. With just folder 4 run, that's `default\sharedcert` only (10 objects, all created in `default`). Folder 4b additionally creates one in `test-domain-01`, so after running it Discovery should also surface `test-domain-01\sharedcert`.

After running folders 1-6 (not 4b), Discovery should return **12 store paths**: 10 `{domain}\cert` (one per test domain) + 1 `default\pubcert` + 1 `default\sharedcert`. Running folder 4b adds a 13th: `test-domain-01\sharedcert`.

## Files

| File | Purpose |
|------|---------|
| `generate-test-certs.ps1` | Generates 120 unique cert+key pairs as Postman iteration-data JSON files under `data/` |
| `DataPower-Test-Setup.postman_collection.json` | Postman collection with all the upload operations |
| `DataPower-Test.postman_environment.json` | Environment template (URL + credentials) |
| `data/*.json` | Generated iteration-data files (gitignored) |

## Setup

### 1. Generate the test certs

```powershell
cd test
pwsh -File generate-test-certs.ps1
```

This writes iteration-data files into `test/data/`:

| File | Rows | Columns | Used by |
|------|------|---------|---------|
| `pubcert-data.json` | 10 | `certPemB64` | folder 3 |
| `sharedcert-data.json` | 10 | `certPemB64`, `keyPemB64` | folder 4 |
| `sharedcert-gap-data.json` | 1 | `crossDomainCertPemB64`, `orphanCertPemB64`, `badP12B64` | folder 4b |
| `perdomain-data.json` | 100 | `certPemB64`, `keyPemB64` | folder 5 |

### 2. Import into Postman

1. **Import collection**: Postman -> Import -> `DataPower-Test-Setup.postman_collection.json`
2. **Import environment**: Postman -> Import -> `DataPower-Test.postman_environment.json`
3. **Select the environment** in Postman's top-right dropdown
4. **Set environment variables**:
   - `BASE_URL` -> your DataPower REST API URL (typically `https://your-appliance:5554`)
   - `USERNAME` -> DataPower admin user
   - `PASSWORD` -> DataPower admin password

### 3. Run the folders in order

Use **Collection Runner** (Postman -> Runner) for each folder:

| # | Folder | Iterations | Data file |
|---|--------|------------|-----------|
| 1 | Create Domains | **10** | - |
| 2 | Save Default Domain Config | 1 | - |
| 3 | Populate Pubcert | from data | `data/pubcert-data.json` |
| 4 | Populate Sharedcert | from data | `data/sharedcert-data.json` (4 requests per iteration: filestore PUT cert, filestore PUT key, POST CryptoCertificate in `default`, POST CryptoKey in `default`) |
| 4b | Populate Sharedcert Gap Cases | **1** | `data/sharedcert-gap-data.json` — see below |
| 5 | Populate Per-Domain Cert Directory | from data | `data/perdomain-data.json` (4 requests per iteration: filestore PUT cert, filestore PUT key, POST CryptoCertificate, POST CryptoKey) |
| 6 | Save All Domains | **10** | - |
| 7 | Verify | 1 | - |

For each folder:
1. Click "Runner" in Postman
2. Drag the folder into the runner
3. If the table above lists a data file, drop it into the "Data" slot — Iterations auto-fills from the row count
4. Otherwise set "Iterations" to the value above
5. Click "Run"

### 4. Verify

The "Verify" folder has GET requests that mirror the Discovery job's calls:

- `GET /mgmt/domains/config/` - should return all 10 test-domain-XX entries (plus default and any pre-existing)
- `GET /mgmt/filestore/test-domain-01` - should list directory entries including `cert`, `pubcert`, `sharedcert`
- `GET /mgmt/filestore/test-domain-01/cert` - should list 20 files (10 certs + 10 keys)
- `GET /mgmt/config/test-domain-01/CryptoCertificate` - should list 10 CryptoCertificate objects (this is what Inventory actually reads)
- `GET /mgmt/config/test-domain-01/CryptoKey` - should list 10 CryptoKey objects
- `GET /mgmt/filestore/default/pubcert` - should list at least 10 test-pubcert-XX.pem files
- `GET /mgmt/filestore/default/sharedcert` - should list at least 10 test-shared-XX.pem files

If those all return data, run the Discovery job from Keyfactor Command and confirm it surfaces the expected store paths (see the count in "What it creates" above).

### Sharedcert gap cases (folder 4b)

Folder 4b creates three `sharedcert` certs exercising the edges of the per-domain sharedcert design:

| Cert | What it is | Expected behavior |
|------|------------|--------------------|
| `test-shared-gap-crossdomain` | A `sharedcert:///` file with its CryptoCertificate config object created in `test-domain-01`, not `default` | This is the case the per-domain redesign exists to handle correctly. Discovery should surface a separate `test-domain-01\sharedcert` store (alongside `default\sharedcert`), and an Inventory job against `test-domain-01\sharedcert` should return exactly this one cert - not the 10 from `default\sharedcert`. Renewing it (Management Add) should update the object in `test-domain-01`, not create a duplicate under `default`. |
| `test-shared-gap-orphan` | A raw `sharedcert:///` file in `default` with no CryptoCertificate config object at all | Still invisible to both Discovery and Inventory, same as an orphan file would be for `cert`. This is expected, not a bug - sharedcert now follows the same "config objects are the source of truth" rule as cert, and DataPower itself has no domain to attribute an object-less file to. Confirms `default\sharedcert`'s Inventory count doesn't inflate to include it. |
| `test-shared-gap-badp12` | A CryptoCertificate object in `default` pointing at a binary `.p12` file instead of a PEM | DataPower accepts the upload and the config object without complaint, but `GetCerts`' per-cert detail fetch can't parse a raw PKCS#12 blob as a bare X.509 cert. Inventory against `default\sharedcert` should report a **Warning** result (not silent Success) naming `test-shared-gap-badp12` as unresolved, alongside the 10 real certs from folder 4 that did resolve. |

## Test Case Matrix

The table below maps each functional behavior to the automated test(s) that prove it
and, where applicable, what actually happened when it was exercised against a live
appliance in this lab. "Automated" results come from `DataPower.Tests`
(`dotnet test DataPower.Tests/DataPower.Tests.csproj` — 185 tests, all passing as of
this writing) running against a mocked `IDataPowerClient`, so they verify the
orchestrator's logic in isolation. "Live" results come from running the Postman
collection folders in this directory against a real appliance (`20.84.52.84` in this
lab) and, where noted, from actually triggering the job in Keyfactor Command.

> **Reading the Actual Result column:** "Pass (unit)" means the behavior is verified
> by a mocked unit test only — the underlying appliance data may be staged (via the
> Postman folders above) but the Command job itself has not necessarily been
> triggered end-to-end. "Pass (live via Command)" means an operator actually ran that
> job through Command against the lab appliance and the result was observed directly,
> not just tested against a mock.

### Discovery

| TC | Scenario | Expected Result | Verified By | Actual Result |
|----|----------|------------------|-------------|----------------|
| TC-D1 | `cert` discovered per domain | `<domain>\cert` emitted for every domain whose filestore lists a `cert:` directory | `DiscoveryTests.PerformDiscovery_EmitsPerDomainCertAndDefaultOnlyPubcert` | Pass (unit) |
| TC-D2 | `pubcert` discovered under `default` only | `default\pubcert` emitted once; never `<non-default>\pubcert`, even if that domain's filestore listing also shows `pubcert:` | `DiscoveryTests.PerformDiscovery_PubcertInNonDefaultDomainFilestore_IsNotEmittedThere` | Pass (unit) |
| TC-D3 | `sharedcert` discovered per owning domain | `<domain>\sharedcert` emitted only for domains that own a `CryptoCertificate` object referencing a `sharedcert://` file — not for every domain that can merely read the filestore | `DiscoveryTests.PerformDiscovery_SharedcertOnlyEmittedForDomainsOwningAMatchingCryptoCertificateObject` | **Pass (unit) and Pass (live via Command)** — after publishing folder 4b's `test-shared-gap-crossdomain` object into `test-domain-01`, a live Discovery run in Command listed 14 stores total, including `test-domain-01\sharedcert` alongside the 10 `{domain}\cert` stores, `default\cert`, `default\pubcert`, and `default\sharedcert` |
| TC-D4 | Resilient to one domain's filestore listing failing | One domain throwing on `ListFileStoreDirectories` doesn't abort discovery of the rest; the failure is logged and grouped, not fatal | `DiscoveryTests.PerformDiscovery_OneDomainFailingFilestoreListing_DoesNotAbortDiscovery` | Pass (unit) |
| TC-D5 | Resilient to one domain's sharedcert probe failing | Same resilience for the `CryptoCertificate`-ownership probe used for `sharedcert` | `DiscoveryTests.PerformDiscovery_OneDomainFailingSharedcertProbe_DoesNotAbortDiscovery` | Pass (unit) |
| TC-D6 | Empty-named domain skipped | A domain entry with a null/empty `Name` is skipped in both the cert/pubcert pass and the sharedcert-ownership pass, without throwing | `DiscoveryTests.PerformDiscovery_EmptyNamedDomain_IsSkippedInBothCertAndSharedcertPasses` | Pass (unit) |
| TC-D7 | Custom "Directories to search" honored | A `dirs` job property restricts Discovery to only the requested directory types (e.g. `cert` only skips the `sharedcert` CryptoCertificate probe entirely) | `DiscoveryTests.PerformDiscovery_UserDirsToSearch_RestrictsToRequestedDirectories` | Pass (unit) |
| TC-D8 | Falls back to default dirs when property doesn't resolve | A job property dictionary that's present but has no usable `dirs` value falls back to the standard `cert,pubcert,sharedcert` set | `DiscoveryTests.PerformDiscovery_JobPropertiesPresentButNoDirsKeyMatches_FallsBackToDefaultDirs`, `PerformDiscovery_DirsKeyPresentButValueEmpty_FallsThroughToNextKeyThenDefault` | Pass (unit) |
| TC-D9 | No domains returned | An appliance reporting zero domains submits an empty discovery list as `Success`, not a failure | `DiscoveryTests.PerformDiscovery_NoDomainsReturned_SubmitsEmptyListAsSuccess` | Pass (unit) |

### Inventory

| TC | Scenario | Expected Result | Verified By | Actual Result |
|----|----------|------------------|-------------|----------------|
| TC-I1 | Inventory `pubcert` | `default\pubcert` returns one `CurrentInventoryItem` per valid PEM in the filestore, routed through `GetPublicCerts` | `InventoryTests.ProcessJob_PubcertStorePath_RoutesToGetPublicCerts`, `RequestManagerGetCertsTests.GetPublicCerts_ReturnsSubmittedItemsForEachPubFile` | Pass (unit). Underlying data (10 pubcert PEMs) confirmed present on the live appliance via folder 7's `GET /mgmt/filestore/default/pubcert`; the Inventory job itself has not yet been triggered through Command in this session |
| TC-I2 | Inventory `pubcert` respects page size | Only `InventoryPageSize` items are submitted even if more files exist | `RequestManagerGetCertsTests.GetPublicCerts_RespectsPageSizeLimit` | Pass (unit) |
| TC-I3 | Inventory `sharedcert`, per-domain scoped | A `<domain>\sharedcert` store returns only the `CryptoCertificate` objects owned by that domain and matching the `sharedcert:` scheme, via `GetCerts` | `InventoryTests.ProcessJob_SharedcertStorePath_RoutesToGetCerts`, `RequestManagerGetCertsTests.GetCerts_ResolvedCert_IsSubmittedAsSuccess`, `GetCerts_FiltersByStoreScheme` | Pass (unit). Folder 7 confirms 10 matching `CryptoCertificate` objects exist in `default` on the live appliance; Inventory job not yet triggered through Command in this session |
| TC-I4 | Inventory blacklist filtering | An alias listed in `InventoryBlackList` is excluded from submitted items | `RequestManagerGetCertsTests.GetCerts_BlacklistedAlias_IsExcludedFromSubmission` | Pass (unit) |
| TC-I5 | Partial-failure surfaces as `Warning`, not silent `Success` | A cert that fails to resolve (e.g. a `.p12` `GetCerts` can't parse as a bare X.509 cert) downgrades the result to `Warning` and names the unresolved alias, instead of reporting `Success` with a silently-lower item count | `InventoryTests.ProcessJob_UnresolvedCertInGetCerts_ReturnsWarningWithFlowSummaryAppended` | Pass (unit). `test-shared-gap-badp12` is published on the live appliance (folder 4b) to reproduce this; the Inventory job has not yet been run through Command to observe the live `Warning` result |
| TC-I6 | Appliance error surfaces as `Failure` with detail | A `DataPowerApiException` from the appliance propagates as a described `Failure`, not a generic message | `InventoryTests.ProcessJob_ApiExceptionFromClient_ReturnsFailureWithDescribedMessage` | Pass (unit) |
| TC-I7 | Input validation | Null config, null submit delegate, missing `CertificateStoreDetails`, empty `ClientMachine`, empty `StorePath`, and unparsable `Properties` all fail fast with a clear `Failure` rather than throwing an unhandled exception | `InventoryTests.ProcessJob_NullConfig_ReturnsFailure` and 5 related tests | Pass (unit) |

### Management (Add / Remove)

| TC | Scenario | Expected Result | Verified By | Actual Result |
|----|----------|------------------|-------------|----------------|
| TC-M1 | Add/renew `sharedcert`, per-domain routing | The `sharedcert:///` file write always goes through `default` (appliance requirement), but the `CryptoCertificate`/`CryptoKey` config object is created/updated in the domain that store path names | `RequestManagerAddRemoveTests.Add_SharedcertPerDomainStore_RoutesFileWritesToDefaultAndConfigObjectsToOwningDomain` | Pass (unit) |
| TC-M2 | Renewing an existing object updates in place | If a `CryptoCertificate`/`CryptoKey` object with the derived name already exists, Add disables + updates it rather than creating a duplicate | `RequestManagerAddRemoveTests.Add_ExistingCryptoCertificateObject_UpdatesInPlaceInsteadOfCreatingDuplicate`, `Add_EverythingAlreadyExists_ReplacesFilesAndUpdatesBothConfigObjects` | Pass (unit) |
| TC-M3 | PFX (password-protected) content extracts correctly | A real PKCS#12 blob with `PrivateKeyPassword` set is parsed and the extracted cert PEM is what actually gets uploaded | `RequestManagerAddRemoveTests.Add_ValidPfxContents_ExtractsRealCertAndKeySuccessfully`, `RequestManagerAddPubCertTests.AddPubCert_PfxWithPassword_ExtractsCertificateAndSucceeds` | Pass (unit) |
| TC-M4 | Add to `pubcert` on a non-default domain rejected | Rejected before any appliance call, with a clear message, since `pubcert` has no per-domain identity | `RequestManagerAddRemoveTests.Add_PubcertToNonDefaultDomain_IsRejectedBeforeAnyApiCall` | Pass (unit) |
| TC-M5 | Appliance error is not swallowed | An appliance failure during Add propagates as `Failure` and does *not* call `SaveConfig` to persist partial state | `RequestManagerAddRemoveTests.Add_ApplianceReturnsError_PropagatesFailureInsteadOfSwallowingIt`, `Add_KeyFileUploadFails_PropagatesFailureAfterCertFileSucceeded` | Pass (unit) |
| TC-M6 | Remove deletes both the config object and the file | Remove deletes the `CryptoCertificate`/`CryptoKey` object and the underlying file, routing the `sharedcert` file delete through `default` regardless of which domain owns the object | `RequestManagerAddRemoveTests.Remove_DeletesCryptoObjectAndFile_RoutingSharedcertFileDeleteThroughDefault`, `Remove_ExistingCryptoKey_IsDeletedAlongsideTheCertificate` | Pass (unit) |
| TC-M7 | Remove of `pubcert` rejected | `pubcert` cannot be removed via this orchestrator | `RequestManagerAddRemoveTests.Remove_PublicCertStore_IsRejected` | Pass (unit) |
| TC-M8 | Appliance-side failures during helper calls don't abort the job | Failures inside defensive existence-check/disable helpers (e.g. `ViewCertificates` throwing while checking if an object already exists) are logged and swallowed, not propagated as job failures | `RequestManagerHelperMethodTests` (11 tests), `RequestManagerAddRemoveTests.Remove_ViewCryptoCertificateThrows_IsSwallowedAndStillSavesConfig` | Pass (unit) |

### Live Appliance Verification (Postman, this lab)

These rows record what was actually observed running the Postman collection against
the lab appliance (`20.84.52.84:5554`) in this session — distinct from the mocked
unit tests above, which never make a real network call.

| TC | Scenario | Expected Result | Actual Result |
|----|----------|------------------|----------------|
| TC-L1 | Populate baseline data (folders 1-3, 5-7) | 10 domains, 10 pubcert PEMs, 100 per-domain cert+key pairs with matching config objects all created and saved | **Pass** — all requests returned `200`/`201`; folder 7's Verify probes confirmed 10 domains, 20 files in `test-domain-01/cert`, 10 `CryptoCertificate`/`CryptoKey` objects in `test-domain-01`, 10 pubcert files |
| TC-L2 | Populate `default/sharedcert` baseline (folder 4) | 10 unique cert+key pairs uploaded with matching `CryptoCertificate`/`CryptoKey` objects in `default` | **Pass** — all requests returned `200`/`201` |
| TC-L3 | Populate sharedcert gap cases (folder 4b) | Cross-domain object created in `test-domain-01`; orphan file with no config object created in `default/sharedcert`; bad `.p12` file + config object created in `default` | **Pass** — cross-domain object returned `409` on a repeat run (already existed from a prior run — expected), orphan file and bad-`.p12` file/object returned `201` |
| TC-L4 | Discovery reflects per-domain `sharedcert` | `test-domain-01\sharedcert` appears as its own store, distinct from `default\sharedcert` | **Pass (confirmed via Command UI)** — store list showed `Total: 14`, including `test-domain-01\sharedcert` |
| TC-L5 | Inventory of `default\sharedcert` returns a `Warning` for the bad `.p12` | Inventory job reports `Warning`, naming `test-shared-gap-badp12` as unresolved | **Not yet run** — test data is staged (TC-L3), but the Inventory job has not been triggered through Command in this session to observe the result |
| TC-L6 | Renewing `test-shared-gap-crossdomain` updates `test-domain-01`, not `default` | Management Add updates the existing object in `test-domain-01` in place | **Not yet run** — covered by unit test TC-M2/TC-M1 only; not yet exercised via a live Command renewal job |

## Cleanup

To remove the test data when you're done, run the **Cleanup (optional)** folder with 10 iterations. This deletes the 10 test domains and the 20 appliance-wide cert files (pubcert + sharedcert). Files inside `{domain}/cert` are removed automatically when the domain is deleted.

> **Notes:**
> - Files inside per-domain `cert/` and the per-domain `CryptoCertificate` / `CryptoKey` objects are removed implicitly when the test domain is deleted, so the Cleanup folder doesn't enumerate them.
> - The `default` domain's `CryptoCertificate` / `CryptoKey` objects (created by folder 4 for sharedcert, plus the bad-.p12 object from folder 4b) are NOT cascaded — Cleanup deletes them explicitly. Same for the appliance-wide pubcert / sharedcert filestore entries.

## Troubleshooting

| Symptom | Likely cause |
|---------|--------------|
| 401 Unauthorized | Check `USERNAME` / `PASSWORD` env vars; verify the user has REST Management Interface access |
| 404 on `/mgmt/domains/config/` | REST mgmt interface may not be enabled on your DataPower; check `xml-mgmt` config |
| 409 Conflict on domain create | Domain already exists - either delete it first or skip iteration |
| 400 with "duplicate" on filestore PUT | File already exists with that name - delete first or use a different filename pattern |
| Empty cert/key on upload | Data file wasn't attached in the Runner. Re-run with the matching `data/*.json` selected. |
| Discovery returns 0 results | Check the orchestrator log for errors. Verify with the GET endpoints in folder 7. |

## Notes

- The certs are self-signed and intended for **lab use only**. Do not expose this appliance publicly.
- Each cert/key uploaded is unique - Inventory results will surface 10 distinct thumbprints per directory, which exercises duplicate-detection paths in the orchestrator more thoroughly than reusing one cert.
- Re-running `generate-test-certs.ps1` overwrites the data files; the certs in your appliance keep whatever was uploaded most recently.
