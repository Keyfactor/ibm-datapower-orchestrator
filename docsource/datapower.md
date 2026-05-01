#### 🔐 Purpose

This store type manages certificates on IBM DataPower appliances. A single store type covers all three DataPower certificate-storage models — per-domain `cert:`, appliance-wide `pubcert:`, and appliance-wide `sharedcert:` — by branching on the store path's directory at runtime.

| Path shape | What it manages | Typical use |
|------------|-----------------|-------------|
| `<domain>\cert` | A specific domain's `CryptoCertificate` / `CryptoKey` configuration objects, plus the underlying PEM/key files in that domain's `cert:` filestore. | Per-application TLS identity. Each business service / tenant / domain has its own certs, isolated from other domains. |
| `default\pubcert` | Public certificates in the appliance's `pubcert:` filestore. | Trust anchors — CA roots and partner public certs the appliance uses to verify outbound TLS, signed assertions, and similar. |
| `default\sharedcert` | Appliance-wide certificates in `sharedcert:`, exposed through the `default` domain's `CryptoCertificate` objects. | Identity certs that survive firmware refreshes — the management-interface TLS cert, signing certs every domain reuses, etc. |

`pubcert` and `sharedcert` are physically a single store on the appliance, owned by `default`. Other domains can read them through their filestore view, but DataPower rejects writes through any non-default domain context with `HTTP 403`. The orchestrator enforces this up front: Add / Remove against `<non-default>\pubcert` or `<non-default>\sharedcert` is rejected with a clear failure message.

#### Prerequisites Specific to This Store Type

Before approving a discovered store or creating one manually:

1. **REST Management Interface enabled** on the target appliance. From the DataPower CLI:
   ```
   web-mgmt
     admin-state enabled
     port 9090
   exit
   xml-mgmt
     admin-state enabled
     port 5554
   exit
   ```
   The port `5554` is what goes into Client Machine.
2. **API user with Crypto Configuration access** in the target domain(s). Read-only is sufficient for Discovery and Inventory; Add and Remove require write on `CryptoCertificate`, `CryptoKey`, and the relevant filestore directories.
3. **Outbound network reachability** from the orchestrator host to the appliance over HTTPS on the REST mgmt port.

Verify access from the orchestrator host with a quick probe (replace credentials as appropriate; `-k` skips cert verification for lab appliances with self-signed mgmt certs):

```bash
curl -k -u admin:PASSWORD https://datapower.example.com:5554/mgmt/domains/config/
```

If this returns a JSON list of domains, the orchestrator will work from this host with these credentials.

#### Operational Notes

- **Discovery vs manual creation.** Discovery emits exactly the store paths the orchestrator can manage: `<domain>\cert` per domain plus `default\pubcert` and `default\sharedcert` once each. Approving discovered paths is preferred over manual entry — it sidesteps typo-driven mismatches and the orphan stores left over from older Discovery emit shapes.
- **Inventory pagination.** The default page size of `100` is appropriate for typical DataPower appliances. Increase only if a single domain exceeds 100 `CryptoCertificate` objects and you want them in a single Inventory pass.
- **Black-list filtering.** `Inventory Black List` accepts comma-separated alias names (e.g. `system-cert,internal-test`). Matching is case-insensitive. Use this to keep system-managed certs out of Command's inventory.
- **FlowLogger summary.** Every job result — success or failure — has a multi-line `Flow: <Job>-ProcessJob` breadcrumb appended to its `FailureMessage`. The summary lists every step with timing and error reason, visible in Command's job-history pane without needing Trace logging on the agent. Useful entries to look for:
  - `GetCerts.ParseResponse - certCount=N (filtered from M by scheme '<store>:')` confirms the response was received and how many entries survived the URI-scheme filter.
  - `GetCerts.SubmitInventory - itemCount=N` shows how many made it through per-cert detail fetch.
  - `ResolveDirsToSearch - source=user|default, dirs=[...]` (Discovery only) confirms which "Directories to search" list was applied.

#### Common Errors

| Symptom | Most likely cause |
|---------|-------------------|
| `HTTP 403 Forbidden` on Add to `<non-default>\sharedcert` or `<non-default>\pubcert` | Target store is appliance-wide. Use `default\pubcert` / `default\sharedcert` instead. The orchestrator now rejects this up front with a clearer message. |
| Inventory returns 0 items but the appliance has certs | The store path may reference a directory whose URI scheme doesn't match the directory name (rare). Check `Get Certs Response` in the orchestrator log to see what `Filename` values the appliance returned. |
| `The specified certificate has an unreadable, corrupt, or invalid certificate file` on Inventory's per-cert detail fetch | DataPower's parser rejected the cert file. Common cause is a self-signed cert lacking standard X.509 extensions (BasicConstraints, KeyUsage). The `test/generate-test-certs.ps1` script in the repo generates lab certs with the right extensions for testing. |
| `401 Unauthorized` | API credentials are wrong, or the REST mgmt user lacks access to the target domain. |
| `404 Not Found` on `/mgmt/domains/config/` | REST mgmt interface is not enabled on the appliance, or the orchestrator is pointing at the wrong port. |
