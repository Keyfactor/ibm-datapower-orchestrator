<h1 align="center" style="border-bottom: none">
    DataPower Universal Orchestrator Extension
</h1>

<p align="center">
  <!-- Badges -->
<img src="https://img.shields.io/badge/integration_status-production-3D1973?style=flat-square" alt="Integration Status: production" />
<a href="https://github.com/Keyfactor/ibm-datapower-orchestrator/releases"><img src="https://img.shields.io/github/v/release/Keyfactor/ibm-datapower-orchestrator?style=flat-square" alt="Release" /></a>
<img src="https://img.shields.io/github/issues/Keyfactor/ibm-datapower-orchestrator?style=flat-square" alt="Issues" />
<img src="https://img.shields.io/github/downloads/Keyfactor/ibm-datapower-orchestrator/total?style=flat-square&label=downloads&color=28B905" alt="GitHub Downloads (all assets, all releases)" />
</p>

<p align="center">
  <!-- TOC -->
  <a href="#support">
    <b>Support</b>
  </a>
  ·
  <a href="#installation">
    <b>Installation</b>
  </a>
  ·
  <a href="#license">
    <b>License</b>
  </a>
  ·
  <a href="https://github.com/orgs/Keyfactor/repositories?q=orchestrator">
    <b>Related Integrations</b>
  </a>
</p>

## Overview

The IBM DataPower Universal Orchestrator manages certificates on IBM DataPower appliances. It targets the appliance's REST Management Interface (typically port `5554`) and uses the same store-path model across every job type: `<domain>\<directory>`.

```mermaid
flowchart LR
    A[Keyfactor Command] -->|Discovery / Inventory / Add / Remove| B[Orchestrator]
    B -->|HTTPS REST| C[DataPower REST Mgmt]
    C -->|domains, filestore, CryptoCertificate, CryptoKey| D[(DataPower Appliance)]
```

## Compatibility

This integration is compatible with Keyfactor Universal Orchestrator version 10.4 and later.

## Support

The DataPower Universal Orchestrator extension is supported by Keyfactor. If you require support for any issues or have feature request, please open a support ticket by either contacting your Keyfactor representative or via the Keyfactor Support Portal at https://support.keyfactor.com.

> If you want to contribute bug fixes or additional enhancements, use the **[Pull requests](../../pulls)** tab.

## Requirements & Prerequisites

Before installing the DataPower Universal Orchestrator extension, we recommend that you install [kfutil](https://github.com/Keyfactor/kfutil). Kfutil is a command-line tool that simplifies the process of creating store types, installing extensions, and instantiating certificate stores in Keyfactor Command.

## DataPower Certificate Store Type

To use the DataPower Universal Orchestrator extension, you **must** create the DataPower Certificate Store Type. This only needs to happen _once_ per Keyfactor Command instance.

TODO Overview is a required section

#### Supported Operations

| Operation    | Is Supported |
|--------------|--------------|
| Add          | ✅ Checked |
| Remove       | 🔲 Unchecked |
| Discovery    | ✅ Checked |
| Reenrollment | 🔲 Unchecked |
| Create       | 🔲 Unchecked |

#### Store Type Creation

##### Using kfutil:
`kfutil` is a custom CLI for the Keyfactor Command API and can be used to create certificate store types.
For more information on [kfutil](https://github.com/Keyfactor/kfutil) check out the [docs](https://github.com/Keyfactor/kfutil?tab=readme-ov-file#quickstart)

   <details><summary>Click to expand DataPower kfutil details</summary>

   ##### Using online definition from GitHub:
   This will reach out to GitHub and pull the latest store-type definition
   ```shell
   # IBM Data Power
   kfutil store-types create DataPower
   ```

   ##### Offline creation using integration-manifest file:
   If required, it is possible to create store types from the [integration-manifest.json](./integration-manifest.json) included in this repo.
   You would first download the [integration-manifest.json](./integration-manifest.json) and then run the following command
   in your offline environment.
   ```shell
   kfutil store-types create --from-file integration-manifest.json
   ```
   </details>

#### Manual Creation
Below are instructions on how to create the DataPower store type manually in
the Keyfactor Command Portal

   <details><summary>Click to expand manual DataPower details</summary>

   Create a store type called `DataPower` with the attributes in the tables below:

   ##### Basic Tab
   | Attribute | Value | Description |
   | --------- | ----- | ----- |
   | Name | IBM Data Power | Display name for the store type (may be customized) |
   | Short Name | DataPower | Short display name for the store type |
   | Capability | DataPower | Store type name orchestrator will register with. Check the box to allow entry of value |
   | Supports Add | ✅ Checked | Indicates that the Store Type supports Management Add |
   | Supports Remove | 🔲 Unchecked | Indicates that the Store Type supports Management Remove |
   | Supports Discovery | ✅ Checked | Indicates that the Store Type supports Discovery |
   | Supports Reenrollment | 🔲 Unchecked | Indicates that the Store Type supports Reenrollment |
   | Supports Create | 🔲 Unchecked | Indicates that the Store Type supports store creation |
   | Needs Server | ✅ Checked | Determines if a target server name is required when creating store |
   | Blueprint Allowed | 🔲 Unchecked | Determines if store type may be included in an Orchestrator blueprint |
   | Uses PowerShell | 🔲 Unchecked | Determines if underlying implementation is PowerShell |
   | Requires Store Password | 🔲 Unchecked | Enables users to optionally specify a store password when defining a Certificate Store. |
   | Supports Entry Password | 🔲 Unchecked | Determines if an individual entry within a store can have a password. |

   The Basic tab should look like this:

   ![DataPower Basic Tab](docsource/images/DataPower-basic-store-type-dialog.svg)

   ##### Advanced Tab
   | Attribute | Value | Description |
   | --------- | ----- | ----- |
   | Supports Custom Alias | Required | Determines if an individual entry within a store can have a custom Alias. |
   | Private Key Handling | Optional | This determines if Keyfactor can send the private key associated with a certificate to the store. |
   | PFX Password Style | Default | 'Default' - PFX password is randomly generated, 'Custom' - PFX password may be specified when the enrollment job is created (Requires the Allow Custom Password application setting to be enabled.) |

   The Advanced tab should look like this:

   ![DataPower Advanced Tab](docsource/images/DataPower-advanced-store-type-dialog.svg)

   > For Keyfactor **Command versions 24.4 and later**, a Certificate Format dropdown is available with PFX and PEM options. Ensure that **PFX** is selected, as this determines the format of new and renewed certificates sent to the Orchestrator during a Management job. Currently, all Keyfactor-supported Orchestrator extensions support only PFX.

   ##### Custom Fields Tab
   Custom fields operate at the certificate store level and are used to control how the orchestrator connects to the remote target server containing the certificate store to be managed. The following custom fields should be added to the store type:

   | Name | Display Name | Description | Type | Default Value/Options | Required |
   | ---- | ------------ | ---- | --------------------- | -------- | ----------- |
   | ServerUsername | Server Username | Api UserName for DataPower. (or valid PAM key if the username is stored in a KF Command configured PAM integration). | Secret |  | 🔲 Unchecked |
   | ServerPassword | Server Password | A password for DataPower API access.  Used for inventory.(or valid PAM key if the password is stored in a KF Command configured PAM integration). | Secret |  | 🔲 Unchecked |
   | ServerUseSsl | Use SSL | Should be true, http is not supported. | Bool | true | ✅ Checked |
   | InventoryBlackList | Inventory Black List | Comma seperated list of alias values you do not want to inventory from DataPower. | String |  | 🔲 Unchecked |
   | Protocol | Protocol Name | Comma seperated list of alias values you do not want to inventory from DataPower. | String | https | ✅ Checked |
   | PublicCertStoreName | Public Cert Store Name | This probably will remain pubcert unless someone changed the default name in DataPower. | String | pubcert | ✅ Checked |
   | InventoryPageSize | Inventory Page Size | This determines the page size during the inventory calls. (100 should be fine). | String | 100 | ✅ Checked |

   The Custom Fields tab should look like this:

   ![DataPower Custom Fields Tab](docsource/images/DataPower-custom-fields-store-type-dialog.svg)

   ###### Server Username
   Api UserName for DataPower. (or valid PAM key if the username is stored in a KF Command configured PAM integration).


   > [!IMPORTANT]
   > This field is created by the `Needs Server` on the Basic tab, do not create this field manually.


   ###### Server Password
   A password for DataPower API access.  Used for inventory.(or valid PAM key if the password is stored in a KF Command configured PAM integration).


   > [!IMPORTANT]
   > This field is created by the `Needs Server` on the Basic tab, do not create this field manually.


   ###### Use SSL
   Should be true, http is not supported.

   ![DataPower Custom Field - ServerUseSsl](docsource/images/DataPower-custom-field-ServerUseSsl-dialog.svg)
   ![DataPower Custom Field - ServerUseSsl](docsource/images/DataPower-custom-field-ServerUseSsl-validation-options-dialog.svg)


   ###### Inventory Black List
   Comma seperated list of alias values you do not want to inventory from DataPower.

   ![DataPower Custom Field - InventoryBlackList](docsource/images/DataPower-custom-field-InventoryBlackList-dialog.svg)
   ![DataPower Custom Field - InventoryBlackList](docsource/images/DataPower-custom-field-InventoryBlackList-validation-options-dialog.svg)


   ###### Protocol Name
   Comma seperated list of alias values you do not want to inventory from DataPower.

   ![DataPower Custom Field - Protocol](docsource/images/DataPower-custom-field-Protocol-dialog.svg)
   ![DataPower Custom Field - Protocol](docsource/images/DataPower-custom-field-Protocol-validation-options-dialog.svg)


   ###### Public Cert Store Name
   This probably will remain pubcert unless someone changed the default name in DataPower.

   ![DataPower Custom Field - PublicCertStoreName](docsource/images/DataPower-custom-field-PublicCertStoreName-dialog.svg)
   ![DataPower Custom Field - PublicCertStoreName](docsource/images/DataPower-custom-field-PublicCertStoreName-validation-options-dialog.svg)


   ###### Inventory Page Size
   This determines the page size during the inventory calls. (100 should be fine).

   ![DataPower Custom Field - InventoryPageSize](docsource/images/DataPower-custom-field-InventoryPageSize-dialog.svg)
   ![DataPower Custom Field - InventoryPageSize](docsource/images/DataPower-custom-field-InventoryPageSize-validation-options-dialog.svg)


   </details>

## Installation

1. **Download the latest DataPower Universal Orchestrator extension from GitHub.**

    Navigate to the [DataPower Universal Orchestrator extension GitHub version page](https://github.com/Keyfactor/ibm-datapower-orchestrator/releases/latest). Refer to the compatibility matrix below to determine which asset should be downloaded. Then, click the corresponding asset to download the zip archive.

   | Universal Orchestrator Version | Latest .NET version installed on the Universal Orchestrator server | `rollForward` condition in `Orchestrator.runtimeconfig.json` | `ibm-datapower-orchestrator` .NET version to download |
   | --------- | ----------- | ----------- | ----------- |
   | Older than `11.0.0` | | | `net6.0` |
   | Between `11.0.0` and `11.5.1` (inclusive) | `net6.0` | | `net6.0` |
   | Between `11.0.0` and `11.5.1` (inclusive) | `net8.0` | `Disable` | `net6.0` |
   | Between `11.0.0` and `11.5.1` (inclusive) | `net8.0` | `LatestMajor` | `net8.0` |
   | `11.6` _and_ newer | `net8.0` | | `net8.0` |
   | `25.5` _and_ newer | `net10.0` | | `net10.0` |

    Unzip the archive containing extension assemblies to a known location.

    > **Note** If you don't see an asset with a corresponding .NET version, you should always assume that it was compiled for `net10.0`.

2. **Locate the Universal Orchestrator extensions directory.**

    * **Default on Windows** - `C:\Program Files\Keyfactor\Keyfactor Orchestrator\extensions`
    * **Default on Linux** - `/opt/keyfactor/orchestrator/extensions`

3. **Create a new directory for the DataPower Universal Orchestrator extension inside the extensions directory.**

    Create a new directory called `ibm-datapower-orchestrator`.
    > The directory name does not need to match any names used elsewhere; it just has to be unique within the extensions directory.

4. **Copy the contents of the downloaded and unzipped assemblies from __step 2__ to the `ibm-datapower-orchestrator` directory.**

5. **Restart the Universal Orchestrator service.**

    Refer to [Starting/Restarting the Universal Orchestrator service](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/StarttheService.htm).

6. **(optional) PAM Integration**

    The DataPower Universal Orchestrator extension is compatible with all supported Keyfactor PAM extensions to resolve PAM-eligible secrets. PAM extensions running on Universal Orchestrators enable secure retrieval of secrets from a connected PAM provider.

    To configure a PAM provider, [reference the Keyfactor Integration Catalog](https://keyfactor.github.io/integrations-catalog/content/pam) to select an extension and follow the associated instructions to install it on the Universal Orchestrator (remote).

> The above installation steps can be supplemented by the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/CustomExtensions.htm?Highlight=extensions).

## Defining Certificate Stores

### Store Creation

#### Manually with the Command UI

<details><summary>Click to expand details</summary>

1. **Navigate to the _Certificate Stores_ page in Keyfactor Command.**

    Log into Keyfactor Command, toggle the _Locations_ dropdown, and click _Certificate Stores_.

2. **Add a Certificate Store.**

    Click the Add button to add a new Certificate Store. Use the table below to populate the **Attributes** in the **Add** form.

   | Attribute | Description |
   | --------- | ----------- |
   | Category | Select "IBM Data Power" or the customized certificate store name from the previous step. |
   | Container | Optional container to associate certificate store with. |
   | Client Machine | The Client Machine field should contain the IP or Domain name and Port Needed for REST API Access.  For SSH Access, Port 22 will be used. |
   | Store Path | The store path uses the format domain\directory (e.g., default\pubcert, production-api\cert). The Discovery job can automatically find all valid store paths on an appliance. |
   | Orchestrator | Select an approved orchestrator capable of managing `DataPower` certificates. Specifically, one with the `DataPower` capability. |
   | ServerUsername | Api UserName for DataPower. (or valid PAM key if the username is stored in a KF Command configured PAM integration). |
   | ServerPassword | A password for DataPower API access.  Used for inventory.(or valid PAM key if the password is stored in a KF Command configured PAM integration). |
   | ServerUseSsl | Should be true, http is not supported. |
   | InventoryBlackList | Comma seperated list of alias values you do not want to inventory from DataPower. |
   | Protocol | Comma seperated list of alias values you do not want to inventory from DataPower. |
   | PublicCertStoreName | This probably will remain pubcert unless someone changed the default name in DataPower. |
   | InventoryPageSize | This determines the page size during the inventory calls. (100 should be fine). |

</details>

#### Using kfutil CLI

<details><summary>Click to expand details</summary>

1. **Generate a CSV template for the DataPower certificate store**

    ```shell
    kfutil stores import generate-template --store-type-name DataPower --outpath DataPower.csv
    ```
2. **Populate the generated CSV file**

    Open the CSV file, and reference the table below to populate parameters for each **Attribute**.

   | Attribute | Description |
   | --------- | ----------- |
   | Category | Select "IBM Data Power" or the customized certificate store name from the previous step. |
   | Container | Optional container to associate certificate store with. |
   | Client Machine | The Client Machine field should contain the IP or Domain name and Port Needed for REST API Access.  For SSH Access, Port 22 will be used. |
   | Store Path | The store path uses the format domain\directory (e.g., default\pubcert, production-api\cert). The Discovery job can automatically find all valid store paths on an appliance. |
   | Orchestrator | Select an approved orchestrator capable of managing `DataPower` certificates. Specifically, one with the `DataPower` capability. |
   | Properties.ServerUsername | Api UserName for DataPower. (or valid PAM key if the username is stored in a KF Command configured PAM integration). |
   | Properties.ServerPassword | A password for DataPower API access.  Used for inventory.(or valid PAM key if the password is stored in a KF Command configured PAM integration). |
   | Properties.ServerUseSsl | Should be true, http is not supported. |
   | Properties.InventoryBlackList | Comma seperated list of alias values you do not want to inventory from DataPower. |
   | Properties.Protocol | Comma seperated list of alias values you do not want to inventory from DataPower. |
   | Properties.PublicCertStoreName | This probably will remain pubcert unless someone changed the default name in DataPower. |
   | Properties.InventoryPageSize | This determines the page size during the inventory calls. (100 should be fine). |

3. **Import the CSV file to create the certificate stores**

    ```shell
    kfutil stores import csv --store-type-name DataPower --file DataPower.csv
    ```

</details>

#### PAM Provider Eligible Fields
<details><summary>Attributes eligible for retrieval by a PAM Provider on the Universal Orchestrator</summary>

If a PAM provider was installed _on the Universal Orchestrator_ in the [Installation](#Installation) section, the following parameters can be configured for retrieval _on the Universal Orchestrator_.

   | Attribute | Description |
   | --------- | ----------- |
   | ServerUsername | Api UserName for DataPower. (or valid PAM key if the username is stored in a KF Command configured PAM integration). |
   | ServerPassword | A password for DataPower API access.  Used for inventory.(or valid PAM key if the password is stored in a KF Command configured PAM integration). |

Please refer to the **Universal Orchestrator (remote)** usage section ([PAM providers on the Keyfactor Integration Catalog](https://keyfactor.github.io/integrations-catalog/content/pam)) for your selected PAM provider for instructions on how to load attributes orchestrator-side.
> Any secret can be rendered by a PAM provider _installed on the Keyfactor Command server_. The above parameters are specific to attributes that can be fetched by an installed PAM provider running on the Universal Orchestrator server itself.

</details>

> The content in this section can be supplemented by the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/ReferenceGuide/Certificate%20Stores.htm?Highlight=certificate%20store).

## Discovering Certificate Stores with the Discovery Job

Discovery enumerates all domains on the appliance, lists each domain's filestore, and emits a store path for every certificate-relevant directory.

### How It Works

1. **Enumerate domains** — `GET /mgmt/domains/config/` returns every application domain on the appliance.
2. **Resolve directory filter** — the comma-separated **Directories to search** field on the Discovery job is parsed; if blank, the orchestrator falls back to `cert,pubcert,sharedcert`. Trailing colons (`cert:`) are stripped before matching.
3. **List directories per domain** — `GET /mgmt/filestore/{domain}` returns every filestore *location*. The trailing-colon names returned by DataPower are matched against the resolved filter.
4. **Emit store paths** — `<domain>\cert` for every domain that has a `cert` directory; `default\pubcert` and `default\sharedcert` once each (other domains' views of those are skipped because they alias the same physical data).
5. **Submit to Command** — the discovered paths are sent back via `SubmitDiscoveryUpdate` for operator approval.

The orchestrator is resilient to one inaccessible domain: it logs a warning and continues with the rest.

### Configuration

Discovery only needs the appliance connection details — no store path is required:

| Field | Description |
|-------|-------------|
| **Client Machine** | DataPower appliance hostname/IP and REST mgmt port (e.g. `datapower.example.com:5554`) |
| **Server Username** | API username for DataPower (PAM-eligible) |
| **Server Password** | API password (PAM-eligible) |
| **Directories to search** | Comma-separated list of directory names to filter against (e.g. `cert,pubcert,sharedcert`). Leave blank to use the standard set. Custom DataPower scheme names can be included. |

The FlowLogger summary on the job's result records which filter list was applied:

```
[OK] ResolveDirsToSearch - source=user (key=dirs), dirs=[cert,sharedcert]
```

vs

```
[OK] ResolveDirsToSearch - source=default, dirs=[cert,pubcert,sharedcert]
```


## Store Path Format

Every Inventory, Management (Add / Remove), and Discovery operation uses the same path shape:

```
<domain>\<directory>
```

| Part | Description | Examples |
|------|-------------|----------|
| **Domain** | A DataPower application domain. Every appliance has at least `default`; additional domains are created for environment / application isolation. | `default`, `production-api`, `staging` |
| **Directory** | The certificate store directory within that domain. | `cert`, `pubcert`, `sharedcert` |

### Per-Domain vs Appliance-Wide

Two of the three directories are **appliance-wide**: every domain can read them, but they are physically a single store owned by the `default` domain. Mutations (Add / Remove) through any non-default domain context are rejected by DataPower with `HTTP 403 Forbidden`. Discovery and the orchestrator's Add path enforce this:

| Directory | Scope | Discovery emits as | Contents |
|-----------|-------|--------------------|----------|
| `cert` | Per-domain | `<domain>\cert` (one per domain) | Domain-specific certificates and private keys, exposed as `CryptoCertificate` / `CryptoKey` configuration objects in that domain |
| `pubcert` | Appliance-wide | `default\pubcert` (once per appliance) | Public / trusted CA certificates the appliance uses to verify other parties |
| `sharedcert` | Appliance-wide | `default\sharedcert` (once per appliance) | Shared identity certs used by appliance-level services or every domain (e.g. the management-interface TLS cert, an enterprise-wide signing cert) |

So a 10-domain appliance produces **12** discovered store paths (10 × `<domain>\cert` plus `default\pubcert` and `default\sharedcert`), not 30.

> **Add / Remove against `<non-default>\pubcert` or `<non-default>\sharedcert`** is rejected by the orchestrator before the call leaves with `"You can only add to <store> on the default domain"`. This matches DataPower's actual permission model and keeps operators from chasing silent 403s.

## Inventory and Management

Inventory and Add / Remove jobs target a specific store path. The orchestrator branches on the directory:

- `<domain>\cert` and `default\sharedcert` → reads `CryptoCertificate` config objects from `/mgmt/config/{domain}/CryptoCertificate`, filters to those whose `Filename` URI scheme matches the store (so a `default\sharedcert` job ignores the `cert:///` and `pubcert:///` entries that share the domain), and submits the certs.
- `default\pubcert` → reads files directly from the `pubcert:` filestore.

Every job emits a `[FLOW:...]` breadcrumb summary that is appended to the `JobResult.FailureMessage` regardless of success or failure. The summary lists every step (Validate, ParseConfig, CreateApiClient, GetCerts.ParseResponse, GetCerts.SubmitInventory, ...) with timing and any error reason. Operators can read it directly from the job-history pane in Command without enabling Trace logging.

### Optional Store Properties

| Property | Description |
|----------|-------------|
| **Inventory Black List** | Comma-separated alias names to exclude from Inventory results (e.g. `system-cert,internal-test`). Case-insensitive. Empty by default. |
| **Inventory Page Size** | Maximum number of certs returned per Inventory submission. Defaults to `100`. |
| **Public Cert Store Name** | Name of the appliance's public-cert directory (default `pubcert`). Override only if the appliance has been re-configured. |
| **Protocol** | `https` (default) or `http`. Use `http` for lab appliances without the REST mgmt TLS profile configured. |

## Migration Note

Earlier releases of this orchestrator emitted `<each-domain>\pubcert` and `<each-domain>\sharedcert` from Discovery — N copies all aliasing the same physical store. If your environment has previously approved any of those non-default entries as cert stores in Command, they are now orphans:

- Inventory against them returns nothing (the underlying objects all point at `<scheme>:///` paths owned by `default`).
- Add and Remove are rejected by the orchestrator with a clear message.

Re-run Discovery, approve the canonical `default\pubcert` and `default\sharedcert`, and remove the duplicates from your Command instance.

## License

Apache License 2.0, see [LICENSE](LICENSE).

## Related Integrations

See all [Keyfactor Universal Orchestrator extensions](https://github.com/orgs/Keyfactor/repositories?q=orchestrator).
