## Overview

The IBM DataPower Universal Orchestrator manages certificates on IBM DataPower appliances over the REST Management Interface. It supports Discovery, Inventory, Add, and Remove for per-domain (`cert:`), per-domain-with-appliance-wide-storage (`sharedcert:`), and appliance-wide (`pubcert:`) certificate stores. `sharedcert:` files are physically stored appliance-wide, but the `CryptoCertificate` / `CryptoKey` config objects that reference them are domain-scoped, so the orchestrator discovers and manages `sharedcert` per owning domain rather than folding it into `default`.

## Vendor Configuration

Before installing the orchestrator extension:

1. Enable the **REST Management Interface** on the DataPower appliance (typically port `5554`). The orchestrator does not support the legacy XML-Mgmt SOAP interface.
2. Provision a DataPower user with REST mgmt access and the **Crypto Configuration** privileges needed to read and create `CryptoCertificate` / `CryptoKey` configuration objects in every target domain. Read-only is sufficient for Discovery and Inventory; Add and Remove require write.
3. The orchestrator host must reach the appliance over HTTPS on the REST mgmt port. Self-signed appliance certs are accepted (lab use); pin a real cert for production.

See the [DataPower Knowledge Center](https://www.ibm.com/docs/en/datapower-gateway) for instructions on enabling the REST mgmt interface and managing roles.

## License

[Apache 2.0](https://apache.org/licenses/LICENSE-2.0)
