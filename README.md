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

The IBM DataPower Orchestrator allows for the management of certificates in the IBM Datapower platform. Inventory, Add and Remove functions are supported. This integration can add/replace certificates in any domain\directory combination. 

* DataPower



### FortiWeb
TODO Global Store Type Section is an optional section. If this section doesn't seem necessary on initial glance, please delete it. Refer to the docs on [Confluence](https://keyfactor.atlassian.net/wiki/x/SAAyHg) for more info


TODO Overview is a required section

## Compatibility

This integration is compatible with Keyfactor Universal Orchestrator version 10.4 and later.

## Support
The DataPower Universal Orchestrator extension is supported by Keyfactor for Keyfactor customers. If you have a support issue, please open a support ticket with your Keyfactor representative. If you have a support issue, please open a support ticket via the Keyfactor Support Portal at https://support.keyfactor.com. 
 
> To report a problem or suggest a new feature, use the **[Issues](../../issues)** tab. If you want to contribute actual bug fixes or proposed enhancements, use the **[Pull requests](../../pulls)** tab.

## Requirements & Prerequisites

Before installing the DataPower Universal Orchestrator extension, we recommend that you install [kfutil](https://github.com/Keyfactor/kfutil). Kfutil is a command-line tool that simplifies the process of creating store types, installing extensions, and instantiating certificate stores in Keyfactor Command.


The IBM DataPower Orchestrator allows for the management of certificates in the IBM Datapower platform. Inventory, Add and Remove functions are supported.  This integration can add/replace certificates in any domain\directory combination.  For example default\pubcert

### FortiWeb Requirements
TODO Global Store Type Section is an optional section. If this section doesn't seem necessary on initial glance, please delete it. Refer to the docs on [Confluence](https://keyfactor.atlassian.net/wiki/x/SAAyHg) for more info


TODO Requirements is an optional section. If this section doesn't seem necessary on initial glance, please delete it. Refer to the docs on [Confluence](https://keyfactor.atlassian.net/wiki/x/SAAyHg) for more info




## Create the FortiWeb Certificate Store Type

To use the DataPower Universal Orchestrator extension, you **must** create the FortiWeb Certificate Store Type. This only needs to happen _once_ per Keyfactor Command instance.


TODO Global Store Type Section is an optional section. If this section doesn't seem necessary on initial glance, please delete it. Refer to the docs on [Confluence](https://keyfactor.atlassian.net/wiki/x/SAAyHg) for more info


* **Create FortiWeb using kfutil**:

    ```shell
    # FortiWeb
    kfutil store-types create FortiWeb
    ```

* **Create FortiWeb manually in the Command UI**:
    <details><summary>Create FortiWeb manually in the Command UI</summary>

    Create a store type called `FortiWeb` with the attributes in the tables below:

    #### Basic Tab
    | Attribute | Value | Description |
    | --------- | ----- | ----- |
    | Name | FortiWeb | Display name for the store type (may be customized) |
    | Short Name | FortiWeb | Short display name for the store type |
    | Capability | FortiWeb | Store type name orchestrator will register with. Check the box to allow entry of value |
    | Supports Add | ✅ Checked | Check the box. Indicates that the Store Type supports Management Add |
    | Supports Remove | 🔲 Unchecked |  Indicates that the Store Type supports Management Remove |
    | Supports Discovery | 🔲 Unchecked |  Indicates that the Store Type supports Discovery |
    | Supports Reenrollment | 🔲 Unchecked |  Indicates that the Store Type supports Reenrollment |
    | Supports Create | 🔲 Unchecked |  Indicates that the Store Type supports store creation |
    | Needs Server | ✅ Checked | Determines if a target server name is required when creating store |
    | Blueprint Allowed | 🔲 Unchecked | Determines if store type may be included in an Orchestrator blueprint |
    | Uses PowerShell | 🔲 Unchecked | Determines if underlying implementation is PowerShell |
    | Requires Store Password | 🔲 Unchecked | Enables users to optionally specify a store password when defining a Certificate Store. |
    | Supports Entry Password | 🔲 Unchecked | Determines if an individual entry within a store can have a password. |

    The Basic tab should look like this:

    ![FortiWeb Basic Tab](docsource/images/FortiWeb-basic-store-type-dialog.png)

    #### Advanced Tab
    | Attribute | Value | Description |
    | --------- | ----- | ----- |
    | Supports Custom Alias | Required | Determines if an individual entry within a store can have a custom Alias. |
    | Private Key Handling | Optional | This determines if Keyfactor can send the private key associated with a certificate to the store. Required because IIS certificates without private keys would be invalid. |
    | PFX Password Style | Default | 'Default' - PFX password is randomly generated, 'Custom' - PFX password may be specified when the enrollment job is created (Requires the Allow Custom Password application setting to be enabled.) |

    The Advanced tab should look like this:

    ![FortiWeb Advanced Tab](docsource/images/FortiWeb-advanced-store-type-dialog.png)

    #### Custom Fields Tab
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

    ![FortiWeb Custom Fields Tab](docsource/images/FortiWeb-custom-fields-store-type-dialog.png)



    </details>

## Installation

1. **Download the latest DataPower Universal Orchestrator extension from GitHub.** 

    Navigate to the [DataPower Universal Orchestrator extension GitHub version page](https://github.com/Keyfactor/ibm-datapower-orchestrator/releases/latest). Refer to the compatibility matrix below to determine whether the `net6.0` or `net8.0` asset should be downloaded. Then, click the corresponding asset to download the zip archive.
    | Universal Orchestrator Version | Latest .NET version installed on the Universal Orchestrator server | `rollForward` condition in `Orchestrator.runtimeconfig.json` | `ibm-datapower-orchestrator` .NET version to download |
    | --------- | ----------- | ----------- | ----------- |
    | Older than `11.0.0` | | | `net6.0` |
    | Between `11.0.0` and `11.5.1` (inclusive) | `net6.0` | | `net6.0` | 
    | Between `11.0.0` and `11.5.1` (inclusive) | `net8.0` | `Disable` | `net6.0` | 
    | Between `11.0.0` and `11.5.1` (inclusive) | `net8.0` | `LatestMajor` | `net8.0` | 
    | `11.6` _and_ newer | `net8.0` | | `net8.0` |

    Unzip the archive containing extension assemblies to a known location.

    > **Note** If you don't see an asset with a corresponding .NET version, you should always assume that it was compiled for `net6.0`.

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

    To configure a PAM provider, [reference the Keyfactor Integration Catalog](https://keyfactor.github.io/integrations-catalog/content/pam) to select an extension, and follow the associated instructions to install it on the Universal Orchestrator (remote).


> The above installation steps can be supplimented by the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/CustomExtensions.htm?Highlight=extensions).



## Defining Certificate Stores


TODO Global Store Type Section is an optional section. If this section doesn't seem necessary on initial glance, please delete it. Refer to the docs on [Confluence](https://keyfactor.atlassian.net/wiki/x/SAAyHg) for more info

TODO Certificate Store Configuration is an optional section. If this section doesn't seem necessary on initial glance, please delete it. Refer to the docs on [Confluence](https://keyfactor.atlassian.net/wiki/x/SAAyHg) for more info



> The content in this section can be supplimented by the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/ReferenceGuide/Certificate%20Stores.htm?Highlight=certificate%20store).


## Discovering Certificate Stores with the Discovery Job

### FortiWeb Discovery Job
TODO Global Store Type Section is an optional section. If this section doesn't seem necessary on initial glance, please delete it. Refer to the docs on [Confluence](https://keyfactor.atlassian.net/wiki/x/SAAyHg) for more info


TODO Discovery Job Configuration is an optional section. If this section doesn't seem necessary on initial glance, please delete it. Refer to the docs on [Confluence](https://keyfactor.atlassian.net/wiki/x/SAAyHg) for more info



## Test Cases

*** 

#### INVENTORY TEST CASES
Case Number|Case Name|Case Description|Expected Results|Passed
------------|---------|----------------|--------------|----------
1|Pubcert Inventory No Black List Default Domain|Should Inventory Everything in the DataPower pubcert directory on the Default Domain|Keyfactor Inventory Matches pubcert default domain inventory|True
1a|Pubcert Inventory No Black List Default Domain using PAM Credentials|Should Inventory Everything in the DataPower pubcert directory on the Default Domain using credentials stored in a PAM Provider|Keyfactor Inventory Matches pubcert default domain inventory|True
1b|Pubcert Inventory With Black List Default Domain|Should Inventory Everything in the DataPower pubcert directory on the Default Domain Outside of Black List Items ex: Test.pem,Test2.pem|Keyfactor Inventory Matches pubcert default domain inventory outside of Black List Items|True
2|Pubcert Inventory No Black List *testdomain\pubcert* path|Should Inventory Everything in the DataPower pubcert directory on the *testdomain\pubcert* path|Keyfactor Inventory Matches pubcert default domain inventory|True
2a|Pubcert Inventory With Black List *testdomain\pubcert* path|Should Inventory Everything in the DataPower pubcert directory on the *testdomain\pubcert* path Outside of Black List Items ex: Cert1.pem,Cert2.pem|Keyfactor Inventory Matches pubcert default domain inventory outside of Black List Items|True
3|Private Key Cert Inventory No Black List Default Domain|Should Inventory Everything in the DataPower cert directory on the Default Domain|Keyfactor Inventory Matches pubcert default domain inventory|True
3a|Private Key Cert Inventory No Black List Default Domain with Credentials Stored in PAM Provider|Should Inventory Everything in the DataPower cert directory on the Default Domain with Credentials Stored in PAM Provider|Keyfactor Inventory Matches pubcert default domain inventory|True
3b|Private Key Cert Inventory With Black List Default Domain|Should Inventory Everything in the DataPower cert directory on the Default Domain Oustide of Black List Items ex: Test.pem,Test2.pem|Keyfactor Inventory Matches cert default domain inventory outside of Black List Items|True
4|Private Key Cert Inventory No Black List *testdomain\cert* path|Should Inventory Everything in the DataPower cert directory on the  *testdomain\cert* path|Keyfactor Inventory Matches *testdomain\cert* path| inventory|True
4a|Private Key Cert Inventory With Black List *testdomain\cert* path||Should Inventory Everything in the DataPower cert directory on the  *testdomain\cert* path|Keyfactor Inventory Matches *testdomain\cert* path Oustide of Black List Items ex: Test,Test2|Keyfactor Inventory Matches everything in *testdomain\cert* path outside of Black List Items

***

#### ADD/REMOVE TEST CASES
Case Number|Case Name|Case Description|Overwrite Flag|Alias Name|Expected Results|Passed
------------|---------|----------------|--------------|----------|----------------|--------------
1|Pubcert Add with Alias Default Domain|Will create new Cert, Key and Pem/crt entry|False|cryptoobjs|Crypto Key Created, Crypto Cert Created, Pem/Crt created|True
1a|Pubcert Overwrite with Alias Default Domain|Will Replaced Cert, Key and Pem/crt entry|true|cryptoobjs|Crypto Key Replaced, Crypto Cert Replaced, Pem/Crt Replaced|True
1b|Pubcert Add without Alias Default Domain|Will create new Cert, Key and Pem/crt entry with GUID as name|False|cryptoobjs|Crypto Key Created, Crypto Cert Created, Pem/Crt created with GUID as name|True
2|Private Key Add with Alias Default Domain|Will create new Cert, Key and Pem/crt entry|False|cryptoobjs|Crypto Key Created, Crypto Cert Created, Pem/Crt created|True
2a|Private Key Overwrite with Alias Default Domain|Will Replaced Cert, Key and Pem/crt entry|true|cryptoobjs|Crypto Key Replaced, Crypto Cert Replaced, Pem/Crt Replaced|True
2b|Private Key Add without Alias Default Domain|Will create new Cert, Key and Pem/crt entry with GUID as name|False|cryptoobjs|Crypto Key Created, Crypto Cert Created, Pem/Crt created with GUID as name|True
2c|Private Key Cert Add with Alias *testdomain\cert* path|Will create new Cert, Key and Pem/crt entry in *testdomain\cert* path|False|cryptoobjs|Crypto Key Created, Crypto Cert Created, Pem/Crt created in *testdomain\pubcert* path|True
2d|Private Key Cert Add with Alias *testdomain\cert* path|Will create new Cert, Key and Pem/crt entry in *testdomain\cert* path with PAM Credentials|False|cryptoobjs|Crypto Key Created, Crypto Cert Created, Pem/Crt created in *testdomain\pubcert* path gettting credentials from a PAM Provider|True
3a|Private Key Cert Overwrite with Alias *testdomain\cert* path|Will Replaced Cert, Key and Pem/crt entry in *testdomain\cert* path|true|cryptoobjs|Crypto Key Replaced, Crypto Cert Replaced, Pem/Crt Replaced in *testdomain\pubcert* path|True
3b|Private Key Cert Add without Alias *testdomain\cert* path|Will create new Cert, Key and Pem/crt entry with GUID as name in *testdomain\cert* path|False|cryptoobjs|Crypto Key Created, Crypto Cert Created, Pem/Crt created with GUID as name in *testdomain\cert* path|True
4|Remove Private Key and Cert From Default Domain|Remove Private Key and Cert From Default Domain|False|cryptoobjs|Crypto Certificate, Crypto Key and Pem/Crt are removed from Data Power|True
4a|Remove Private Key and Cert From *testdomain\cert* path|Remove Private Key and Cert From *testdomain\cert* path|False|cryptoobjs|Crypto Certificate, Crypto Key and Pem/Crt are removed from Data Power *testdomain\cert* path|True
4b|Remove PubCert|Remove PubCert|False|cryptoobjs|Error Occurs, cannot remove Public Certs|True
4c|Remove Private Key and Cert From *testdomain\cert* path with PAM Credentials|Remove Private Key and Cert From *testdomain\cert* path using credentials stored in a PAM Provider|False|cryptoobjs|Crypto Certificate, Crypto Key and Pem/Crt are removed from Data Power *testdomain\cert* path|True

***


## License

Apache License 2.0, see [LICENSE](LICENSE).

## Related Integrations

See all [Keyfactor Universal Orchestrator extensions](https://github.com/orgs/Keyfactor/repositories?q=orchestrator).