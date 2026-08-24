# DataPower Test Setup

Tools to populate a DataPower test appliance with domains and certificates so you can validate the Discovery and Inventory jobs in the IBM DataPower Orchestrator.

## What it creates

| Resource | Count | Location |
|----------|-------|----------|
| Application domains | 10 | `test-domain-01` through `test-domain-10` |
| Certs in `default/pubcert` | 10 | appliance-wide, visible from every domain (filestore PEMs only) |
| Cert + key files in `default/sharedcert` | 20 | 10 cert + 10 key PEMs |
| `default` CryptoCertificate / CryptoKey objects (sharedcert) | 10 + 10 | each pointing at a `sharedcert:///` PEM |
| Cert + key files in `{domain}/cert` | 200 | 10 cert + 10 key files per domain (10 domains x 20 files) |
| Per-domain CryptoCertificate config objects | 100 | 10 per domain, each pointing at one cert PEM in `cert:///` |
| Per-domain CryptoKey config objects | 100 | 10 per domain, each pointing at one key PEM in `cert:///` |

Every cert and key uploaded is a **unique** self-signed pair, so Inventory results will show distinct thumbprints (no duplicates).

> **Why config objects matter:** for per-domain `cert/` directories the orchestrator's Inventory enumerates `CryptoCertificate` config objects (`/mgmt/config/{domain}/CryptoCertificate`), *not* the filestore. PEMs sitting in `cert:///` without a matching CryptoCertificate object are invisible to Inventory. Folder 5 creates both. Pubcert / sharedcert are read from the filestore directly, so they don't need config objects.

After running, Discovery should return **30 store paths** (10 domains x 3 directories: `cert`, `pubcert`, `sharedcert`).

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
| `sharedcert-gap-data.json` | 1 | `crossDomainCertPemB64`, `orphanCertPemB64` | folder 4b |
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

If those all return data, run the Discovery job from Keyfactor Command and confirm it surfaces the expected 30 store paths.

### Sharedcert gap cases (folder 4b)

Folder 4b deliberately creates two `sharedcert` certs that today's Inventory job (`GetCerts` in `RequestManager.cs`) does **not** return for a `default\sharedcert` store, because it only reads CryptoCertificate config objects in the `default` domain:

| Cert | What it is | Why Inventory misses it today |
|------|------------|-------------------------------|
| `test-shared-gap-crossdomain` | A `sharedcert:///` file with its CryptoCertificate config object created in `test-domain-01` instead of `default` | `GetCerts` only queries `/mgmt/config/default/CryptoCertificate` — objects in other domains are invisible even though the underlying file is appliance-wide |
| `test-shared-gap-orphan` | A raw `sharedcert:///` file with no CryptoCertificate config object at all | `GetCerts` never reads the filestore directly, only config objects |

Running an Inventory job against `default\sharedcert` after populating folder 4b should return the 10 certs from folder 4 but **not** these two — confirming the coverage gap. Once the fix (aggregating CryptoCertificate objects across all domains, and/or reading the filestore directly) is in place, both should appear.

## Cleanup

To remove the test data when you're done, run the **Cleanup (optional)** folder with 10 iterations. This deletes the 10 test domains and the 20 appliance-wide cert files (pubcert + sharedcert). Files inside `{domain}/cert` are removed automatically when the domain is deleted.

> **Notes:**
> - Files inside per-domain `cert/` and the per-domain `CryptoCertificate` / `CryptoKey` objects are removed implicitly when the test domain is deleted, so the Cleanup folder doesn't enumerate them.
> - The `default` domain's `CryptoCertificate` / `CryptoKey` objects (created by folder 4 for sharedcert) are NOT cascaded — Cleanup deletes them explicitly. Same for the appliance-wide pubcert / sharedcert filestore entries.

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
