#### 🔐 Purpose

This store type manages certificates on IBM DataPower appliances. A single store type covers all three DataPower certificate-storage models — per-domain `cert:`, appliance-wide-and-per-domain `sharedcert:`, and appliance-wide `pubcert:` — by branching on the store path's directory at runtime.

| Path shape | What it manages | Typical use |
|------------|-----------------|-------------|
| `<domain>\cert` | A specific domain's `CryptoCertificate` / `CryptoKey` configuration objects, plus the underlying PEM/key files in that domain's `cert:` filestore. | Per-application TLS identity. Each business service / tenant / domain has its own certs, isolated from other domains. |
| `<domain>\sharedcert` | The `CryptoCertificate` / `CryptoKey` configuration objects **owned by that domain** that reference a `sharedcert:///` file. The underlying file itself lives in the appliance-wide `sharedcert:` filestore (physically owned by `default`), but the config object — and therefore which store it's managed through — can belong to any domain. | Identity certs shared or reused across domains (management-interface TLS cert, an enterprise-wide signing cert) where the config object was created outside `default`, as well as the conventional `default\sharedcert` case. |
| `default\pubcert` | Public certificates in the appliance's `pubcert:` filestore, read directly (no config objects involved). | Trust anchors — CA roots and partner public certs the appliance uses to verify outbound TLS, signed assertions, and similar. |

`sharedcert`'s filestore is physically a single store on the appliance owned by
`default` — every domain can read it — but its `CryptoCertificate`/`CryptoKey`
**config objects** are domain-scoped just like `cert`'s. The store type follows the
object's domain, not the filestore's. `pubcert` has no config objects at all, so it
has no per-domain identity to follow and stays `default`-only.

DataPower rejects `sharedcert:///` **filestore writes** through any non-default
domain context with `HTTP 403` — the orchestrator handles this internally by always
routing the file write through `default` regardless of which domain's
`<domain>\sharedcert` store initiated the Add/Renew, while updating the
`CryptoCertificate`/`CryptoKey` object in the domain that actually owns it. `pubcert`
has a store-path-level guard: Add / Remove against `<non-default>\pubcert` is
rejected up front with a clear failure message, since there's no per-domain object
for it to target in the first place.

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

- **Discovery vs manual creation.** Discovery emits exactly the store paths the orchestrator can manage: `<domain>\cert` per domain that has one, `default\pubcert` once, and `<domain>\sharedcert` for every domain that owns a `CryptoCertificate` object referencing a `sharedcert://` file (usually just `default`, but not always — see the Purpose section above). Approving discovered paths is preferred over manual entry — it sidesteps typo-driven mismatches and the orphan stores left over from older Discovery emit shapes.
- **sharedcert domains you don't expect.** If Discovery surfaces a `<domain>\sharedcert` store for an application domain you didn't expect, that means a `CryptoCertificate` object in that domain references a `sharedcert://` file — check `/mgmt/config/{domain}/CryptoCertificate` on the appliance if you want to confirm which object and file before approving it.
- **Inventory pagination.** The default page size of `100` is appropriate for typical DataPower appliances. Increase only if a single domain exceeds 100 `CryptoCertificate` objects and you want them in a single Inventory pass.
- **Black-list filtering.** `Inventory Black List` accepts comma-separated alias names (e.g. `system-cert,internal-test`). Matching is case-insensitive. Use this to keep system-managed certs out of Command's inventory.
- **FlowLogger summary.** Every job result — success or failure — has a multi-line `Flow: <Job>-ProcessJob` breadcrumb appended to its `FailureMessage`. The summary lists every step with timing and error reason, visible in Command's job-history pane without needing Trace logging on the agent. Useful entries to look for:
  - `GetCerts.ParseResponse - certCount=N (filtered from M by scheme '<store>:')` confirms the response was received and how many entries survived the URI-scheme filter.
  - `GetCerts.SubmitInventory - itemCount=N` shows how many made it through per-cert detail fetch.
  - `ResolveDirsToSearch - source=user|default, dirs=[...]` (Discovery only) confirms which "Directories to search" list was applied.

#### Common Errors

| Symptom | Most likely cause |
|---------|-------------------|
| `HTTP 403 Forbidden` on Add to `<non-default>\pubcert` | `pubcert` has no per-domain identity; use `default\pubcert`. The orchestrator rejects this up front with a clearer message before the call reaches the appliance. |
| Inventory for `<domain>\sharedcert` returns fewer certs than expected | Check whether some of the appliance's sharedcert-referencing `CryptoCertificate` objects live in a *different* domain than the one you're inventorying — each is only visible through the `<domain>\sharedcert` store for the domain that actually owns the object. Run Discovery again to see the full set of sharedcert domains. |
| Inventory returns 0 items but the appliance has certs | The store path may reference a directory whose URI scheme doesn't match the directory name (rare). Check `Get Certs Response` in the orchestrator log to see what `Filename` values the appliance returned. |
| `The specified certificate has an unreadable, corrupt, or invalid certificate file` on Inventory's per-cert detail fetch | DataPower's parser rejected the cert file. Common cause is a self-signed cert lacking standard X.509 extensions (BasicConstraints, KeyUsage). The `test/generate-test-certs.ps1` script in the repo generates lab certs with the right extensions for testing. |
| `401 Unauthorized` | API credentials are wrong, or the REST mgmt user lacks access to the target domain. |
| `404 Not Found` on `/mgmt/domains/config/` | REST mgmt interface is not enabled on the appliance, or the orchestrator is pointing at the wrong port. |

## Overview

TODO Overview is a required section

