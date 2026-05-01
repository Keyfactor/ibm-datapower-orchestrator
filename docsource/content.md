## Overview

The IBM DataPower Orchestrator allows for the management of certificates in the IBM DataPower platform. Discovery, Inventory, Add and Remove functions are supported. This integration can manage certificates in any domain and certificate store directory on a DataPower appliance.

* DataPower

## Requirements

The IBM DataPower Orchestrator requires:
- A DataPower appliance with the REST Management Interface enabled (typically port 5554)
- API credentials with access to certificate management operations
- HTTPS connectivity between the Keyfactor Orchestrator and the DataPower appliance

## Store Path Format

The Store Path identifies which domain and certificate store directory to target on the DataPower appliance. All Inventory, Management (Add/Remove), and Discovery operations use this format.

### Format

```
<domain>\<directory>
```

The path is composed of two parts separated by a backslash (`\`) or forward slash (`/`):

| Part | Description | Examples |
|------|-------------|----------|
| **Domain** | The DataPower application domain. Every DataPower appliance has at least a `default` domain. Additional domains are created for environment or application isolation. | `default`, `production-api`, `staging`, `internal-services` |
| **Directory** | The certificate store directory within that domain. DataPower has several standard directories for certificate storage. | `cert`, `pubcert`, `sharedcert` |

### Certificate Store Directories

| Directory | Scope | Contents |
|-----------|-------|----------|
| `cert` | Per-domain | Domain-specific certificates and private keys (CryptoCertificate/CryptoKey objects) |
| `pubcert` | Appliance-wide | Public/trusted certificates shared across all domains |
| `sharedcert` | Appliance-wide | Shared certificates that persist across firmware upgrades |

### Examples

| Store Path | Description |
|------------|-------------|
| `default\pubcert` | Public certificate store in the default domain |
| `default\cert` | Private key certificate store in the default domain |
| `production-api\cert` | Private key certificates in the production-api domain |
| `testdomain\pubcert` | Public certificates in the testdomain domain |

> **Tip:** The Discovery job can automatically find all valid domain and directory combinations on an appliance, eliminating the need to manually determine store paths. See [Discovery](#discovery) below.

## Discovery

The Discovery job automatically enumerates all domains and certificate store directories on a DataPower appliance. This is especially useful for environments with many domains, as it eliminates the need to manually create certificate store definitions.

### How It Works

1. **Enumerate domains** &mdash; calls `GET /mgmt/domains/config/` to list every application domain on the appliance
2. **Discover stores per domain** &mdash; for each domain, calls `GET /mgmt/filestore/{domain}` to list the filestore directories
3. **Filter to certificate directories** &mdash; keeps only certificate-relevant directories (`cert`, `pubcert`, `sharedcert`)
4. **Return store paths** &mdash; submits the discovered paths (e.g., `production-api\cert`) to Keyfactor Command

### Configuration

Discovery requires only the appliance connection details &mdash; no store path is needed:

| Field | Description |
|-------|-------------|
| Client Machine | The DataPower appliance hostname/IP and REST API port (e.g., `datapower.example.com:5554`) |
| Server Username | API username for DataPower (PAM eligible) |
| Server Password | API password for DataPower (PAM eligible) |

### Example

Running Discovery against an appliance with 3 domains returns paths like:

```
default\cert
default\pubcert
production-api\cert
production-api\pubcert
staging\cert
staging\pubcert
```

Each discovered path can become a certificate store definition in Keyfactor Command, ready for Inventory and Management operations.

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


