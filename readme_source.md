**IBM Datapower**

**Overview**

The IBM DataPower Orchestrator allows for the management of certificates in the IBM Datapower platform. Inventory, Add and Remove functions are supported.  This integration can add/replace certificates in any domain\directory combination.  For example default\pubcert

---

﻿**1) Create the new Certificate store Type for the New DataPower AnyAgent**

#### STORE TYPE CONFIGURATION
SETTING TAB  |  CONFIG ELEMENT	| DESCRIPTION
------|-----------|------------------
Basic |Name	|Descriptive name for the Store Type.  IBM Data Power Universal can be used.
Basic |Short Name	|The short name that identifies the registered functionality of the orchestrator. Must be DataPower.
Basic |Custom Capability|Unchecked
Basic |Job Types	|Inventory, Add, and Remove are the supported job types. 
Basic |Needs Server	|Must be checked
Basic |Blueprint Allowed	|checked
Basic |Requires Store Password	|Determines if a store password is required when configuring an individual store.  This must be unchecked.
Basic |Supports Entry Password	|Determined if an individual entry within a store can have a password.  This must be unchecked.
Advanced |Store Path Type| Determines how the user will enter the store path when setting up the cert store.  Freeform
Advanced |Supports Custom Alias	|Determines if an individual entry within a store can have a custom Alias.  Optional (if left blank, alias will be a GUID)
Advanced |Private Key Handling |Determines how the orchestrator deals with private keys.  Optional
Advanced |PFX Password Style |Determines password style for the PFX Password. Default
Custom Fields|Inventory Page Size|Name:InventoryPageSize Display Name:Inventory Page Size Type:String Default Value:100 Required:True.  This determines the page size during the inventory calls. (100 should be fine)
Custom Fields|Public Cert Store Name|Name:PublicCertStoreName Display Name:Public Cert Store Name:String Default Value:pubcert Required:True.  This probably will remain pubcert unless someone changed the default name in DataPower.
Custom Fields|Protocol|Name:Protocol Display Name:Protocol Name:String Default Value:https Required:True.  This should always be https in production, may need to change in test to http.
Custom Fields|Inventory Black List|Name:InventoryBlackList Display Name:Inventory Black List Name:String Default Value:Leave Blank Required:False.  Comma seperated list of alias values you do not want to inventory from DataPower.
Custom Fields|Server Username|Api UserName for DataPower
Custom Fields|Server Password|Api Password for UserName Described Above
Custom Fields|Use SSL|Set this to true
Entry Parameters|N/A| There are no Entry Parameters

![image.png](/images/CertStoreType-Basic.gif)

![image.png](/images/CertStoreType-Advanced.gif)

![image.png](/images/CertStoreType-CustomFields.gif)

    
#### STORE CONFIGURATION 
CONFIG ELEMENT	|DESCRIPTION
----------------|---------------
Category	|The type of certificate store to be configured. Select category based on the display name configured above "IBM Data Power Universal".
Container	|This is a logical grouping of like stores. This configuration is optional and does not impact the functionality of the store.
Client Machine	| The server and port the DataPower API runs on.  This is typically port 5554 for the API.
Store Path	|This will the domain\path combination to enroll and inventory to.  If it is the default domain just put the path.
Inventory Page Size|This determines the page size during the inventory calls. (100 should be fine).
Public Cert Store Name| This probably will remain pubcert unless someone changed the default name in DataPower.
Protocol| This should always be https in production, may need to change in test to http.
Inventory Black List| Comma seperated list of alias values you do not want to inventory from DataPower.
Orchestrator	|This is the orchestrator server registered with the appropriate capabilities to manage this certificate store type. 
Inventory Schedule	|The interval that the system will use to report on what certificates are currently in the store. 
Use SSL	|This should be checked.
User	|The Data Power user that has access to the API and enroll and inventory functions in DataPower.
Password |Password for the user mentioned above.

![image.png](/images/CertStore.gif)

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

### License
[Apache](https://apache.org/licenses/LICENSE-2.0)

