*** 
# Getting Started
## Standard Gateway Installation
To begin, you must have the CA Gateway Service 22.1.0 installed and operational before attempting to configure the CSC Global plugin. This integration was tested with Keyfactor 10.4.0.0.
To install the gateway follow these instructions.

1) Gateway Server - run the installation .msi - Get from Keyfactor

2) Gateway Server - If you have the rights to install the database (usually in a Non SQL PAAS Environment) Using Powershell, run the following command to create the gateway database.

   **SQL Server Windows Auth**
    ```
    %InstallLocation%\DatabaseManagementConsole.exe create -s [database server name] -d [database name]
    ```
   Note if you are using SQL Authentication, then you need to run
   
   **SQL Server SQL Authentication**

   ```
   %InstallLocation%\DatabaseManagementConsole.exe create -s [database server name] -d [database name] -u [sql user] -p [sql password]
   ```

   If you do **not** have rights to created the database then have the database created ahead of time by the support team and just populate the database

   ## Populate commands below

   **Windows Authentication**

   ```
   %InstallLocation%\DatabaseManagementConsole.exe populate -s [database server name] -d [database name]
   ```

   **SQL Server SQL Authentication** 

   ```
   %InstallLocation%\DatabaseManagementConsole.exe populate -s [database server name] -d [database name] -u [sql user] -p [sql password]
   ```

3) Gateway Server - run the following Powershell to import the Cmdlets

   C:\Program Files\Keyfactor\Keyfactor AnyGateway\ConfigurationCmdlets.dll (must be imported into Powershell)
   ```ps
   Import-Module C:\Program Files\Keyfactor\Keyfactor AnyGateway\ConfigurationCmdlets.dll
   ```

4) Gateway Server - Run the Following Powershell script to set the gateway encryption cert

   ### Set-KeyfactorGatewayEncryptionCert
   This cmdlet will generate a self-signed certificate used to encrypt the database connection string. It populates a registry value with the serial number of the certificate to be used. The certificate is stored in the LocalMachine Personal Store and the registry key populated is:

   HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\CertSvcProxy\Parameters\EncryptSerialNumber
   No parameters are required to run this cmdlet.

5) Gateway Server - Run the following Powershell Script to Set the Database Connection

   ### Set-KeyfactorGatewayDatabaseConnection
   This cmdlet will set and encrypt the database connection string used by the AnyGateway service. 

   **Windows Authentication**
   ```ps
   Set-KeyfactorGatewayDatabaseConnection -Server [db server name] -Database [database name]
   ```

   **SQL Authentication**
   ```ps
   $KeyfactorCredentials = Get-Credentials
   Set-KeyfactorGatewayDatabaseConnection -Server [db server name] -Database [database name] -Account [$KeyfactorCredentials]
   ```
## Standard Gateway Configuration Finished
---


## CSC Global AnyGateway Specific Configuration
It is important to note that importing the  CSC Global configuration into the CA Gateway prior to installing the binaries must be completed. Additionally, the CA Gateway service
must be running in order to succesfully import the configuation. When the CA Gateway service starts it will attempt to validate the connection information to 
the CA.  Without the imported configuration, the service will fail to start.

### Binary Installation

1) Get the Latest Zip File from [Here](https://github.com/Keyfactor/cscglobal-cagateway/releases)
2) Gateway Server - Copy the CscGlobalCaProxy.dll to the location where the Gateway Framework was installed (usually C:\Program Files\Keyfactor\Keyfactor AnyGateway)

### Configuration Changes
1) Gateway Server - Edit the CAProxyServer.exe.config file and replace the line that says "NoOp" with the line below:
   ```
   <alias alias="CAConnector" type="Keyfactor.AnyGateway.CscGlobal.CscGlobalCaProxy, CscGlobalCaProxy"/>
   ```
2) Gateway Server - Install the Root CSC Global Certificate that was received from CSC Global 

3) Gateway Server - Install the Intermediate CSC Global Certificate that was received from CSC Global 

4) Gateway Server - Take the sample Config.json located [Here](https://github.com/Keyfactor/cscglobal-cagateway/raw/main/SampleConfig.json) and make the following modifications

- *Security Settings Modifications* (Swap this out for the typical Gateway Security Settings for Test or Prod)

```
  "Security": {
    "KEYFACTOR\\administrator": {
      "READ": "Allow",
      "ENROLL": "Allow",
      "OFFICER": "Allow",
      "ADMINISTRATOR": "Allow"
    },
    "KEYFACTOR\\SVC_AppPool": {
      "READ": "Allow",
      "ENROLL": "Allow",
      "OFFICER": "Allow",
      "ADMINISTRATOR": "Allow"
    },
    "KEYFACTOR\\SVC_TimerService": {
      "READ": "Allow",
      "ENROLL": "Allow",
      "OFFICER": "Allow",
      "ADMINISTRATOR": "Allow"
    }
```
- *CSC Global Environment Settings* (Modify these with the keys and Urls obtained from Csc Global)
```
  "CAConnection": {
    "CscGlobalURL": "https://apis-ote.cscglobal.com/dbs/api/v2",
    "ApiKey": "SALDJDSFKLDFS",
    "BearerToken": "ASDLKFSALDKSDALK",
    "TemplateSync": "On",
    "RenewalWindowDays": "30"
  }
```

`RenewalWindowDays` is optional (default 30) and controls the renewal window described below.

#### Renewal vs. Reissue Logic
When Keyfactor Command submits a **New**, **Renew** or **Reissue** request that identifies a prior certificate, the gateway decides what to order based on the validity of the underlying CSC Global **order**, not the expiration of the certificate itself. This allows shorter-lived certificates (for example ~200 days) issued under a still-active order to be reissued at no cost, and lets renewals of an already expired certificate proceed instead of failing.

1. The order expiry is calculated as the order date of the prior certificate plus one year.
2. If the days remaining until order expiry are **greater than** `RenewalWindowDays` (default 30), the request is processed as a **Reissue** under the active order.
3. If the order has between 0 and `RenewalWindowDays` days remaining, the request is processed as a paid **Renewal** (new order).
4. If the order has already expired, a new certificate is ordered (**New**).
5. If CSC Global does not return an order date, the certificate's own expiration date is used instead: an unexpired certificate is reissued.
6. A request that explicitly asks for a **Reissue** is always processed as a reissue. If the prior certificate cannot be found or evaluated, the requested type is kept. Serial numbers are matched with or without the leading `00` sign byte.

Example with the default of 30 days: an order placed on 1-Jan-2026 expires 1-Jan-2027. A request on 1-Aug-2026 is a reissue, a request on 15-Dec-2026 is a paid renewal, and a request after 1-Jan-2027 orders a new certificate.
**Template Settings**
- For template settings you can either hard code them in the template parameters as shown on the last template or make them show up as enrollment parameters.  You can also have a combination of both enrollment parameters and hard coded parameters in the template parameters.  You can also build a workflow in Keyfactor to change them during enrollment based on some parameters as shown in the attached workflow 1.
```
  "Templates": {
    "CSC TrustedSecure OV": {
      "ProductID": "CSC TrustedSecure OV",
      "Parameters": {}
    },
    "CSC TrustedSecure OV Wildcard": {
      "ProductID": "CSC TrustedSecure OV Wildcard",
      "Parameters": {}
    },
    "CSC TrustedSecure OV - Multiple Names": {
      "ProductID": "CSC TrustedSecure OV - Multiple Names",
      "Parameters": {}
    },
    "CSC TrustedSecure EV": {
      "ProductID": "CSC TrustedSecure EV",
      "Parameters": {}
    },
    "CSC TrustedSecure DV": {
      "ProductID": "CSC TrustedSecure DV",
      "Parameters": {}
    },
    "CSC TrustedSecure DV Wildcard": {
      "ProductID": "CSC TrustedSecure DV Wildcard",
      "Parameters": {}
    },
    "CSC TrustedSecure DV - Multiple Names": {
      "ProductID": "CSC TrustedSecure DV - Multiple Names",
      "Parameters": {
		"Term": "12",
		"Applicant First Name": "Joe",
		"Applicant Last Name": "Smith",
		"Applicant Email Address": "admin@jsmith.com",
		"Applicant Phone (+nn.nnnnnnnn)": "+12.34567890",
		"Domain Control Validation Method": "EMAIL",
		"Organization Contact": "Some Contact",
		"Business Unit": "Some Business Unit",
		"Notification Email(s) Comma Separated": "admin@jsmith.com",
		"CN DCV Email (admin@yourdomain.com)": "admin@jsmith.com",
		"Addtl Sans Comma Separated DVC Emails": "admin@jsmith.com"
	}
    },
    "CSC TrustedSecure EV - Multiple Names": {
      "ProductID": "CSC TrustedSecure EV - Multiple Names",
      "Parameters": {}
    },
    "CSC TrustedSecure OV Wildcard - Multiple Names": {
      "ProductID": "CSC TrustedSecure OV Wildcard - Multiple Names",
      "Parameters": {}
    },
    "CSC TrustedSecure DV Wildcard - Multiple Names": {
      "ProductID": "CSC TrustedSecure DV Wildcard - Multiple Names",
      "Parameters": {}
    }
  }
```

- *Gateway Settings*
```
  "CertificateManagers": null,
  "GatewayRegistration": {
    "LogicalName": "CscGlobal",
    "GatewayCertificate": {
      "StoreName": "CA",
      "StoreLocation": "LocalMachine",
      "Thumbprint": "525c47fb3a5e0655fbd4be963ca1e94d5fecb43d"
    }
  }
```

- *Service Settings* (Modify these to be in accordance with Keyfactor Standard Gateway Production Settings)
```
  "ServiceSettings": {
    "ViewIdleMinutes": 1,
    "FullScanPeriodHours": 1,
    "PartialScanPeriodMinutes": 1
  }
```

5) Gateway Server - Save the newly modified config.json to the following location "C:\Program Files\Keyfactor\Keyfactor AnyGateway"

### Template Installation

Certificate templates and their enrollment fields for CSC Global are set up by Keyfactor Support. Please open a ticket via the Keyfactor Support Portal (https://support.keyfactor.com) and ask for the CSC Global templates to be configured for your environment. Include the products you want to offer and the Keyfactor Command and gateway versions you are running.

**PLEASE NOTE, AT THIS TIME THE RAPID_SSL TEMPLATE IS NOT SUPPORTED BY THE CSC API AND WILL NOT WORK WITH THIS INTEGRATION**

Active Directory template names can't contain commas, so the CSC names that contain a comma are used with a hyphen in templates, for example CSC TrustedSecure OV - Multiple Names. The gateway translates between the two forms and also accepts the comma form.

The gateway supports the following CSC Global certificate types. The product name in the template must match one of these names:

Value | Product Name | Legacy Name (still supported)
------|--------------|------------------------------
0 | CSC TrustedSecure OV | CSC TrustedSecure Premium Certificate
1 | CSC TrustedSecure OV Wildcard | CSC TrustedSecure Premium Wildcard Certificate
2 | CSC TrustedSecure OV - Multiple Names | CSC TrustedSecure UC Certificate
3 | CSC TrustedSecure EV | CSC TrustedSecure EV Certificate
4 | CSC TrustedSecure DV | CSC TrustedSecure Domain Validated SSL
5 | CSC TrustedSecure DV Wildcard | CSC TrustedSecure Domain Validated Wildcard SSL
6 | CSC TrustedSecure DV - Multiple Names | CSC TrustedSecure Domain Validated UC Certificate
7 | CSC TrustedSecure EV - Multiple Names | N/A
8 | CSC TrustedSecure OV Wildcard - Multiple Names | N/A
9 | CSC TrustedSecure DV Wildcard - Multiple Names | N/A

The enrollment fields used by the gateway are: Term, Applicant First Name, Applicant Last Name, Applicant Email Address, Applicant Phone (+nn.nnnnnnnn), Domain Control Validation Method, Organization Contact, Business Unit, Notification Email(s) Comma Separated and CN DCV Email (admin@yourdomain.com). The Multiple Names products also use Addtl Sans Comma Separated DVC Emails, and the EV products also require Organization Country.

### Certificate Authority Installation
1) Gateway Server - Start the Keyfactor Gateway Service
2) Run the set Gateway command similar to below
```ps
Set-KeyfactorGatewayConfig -LogicalName "CSCGlobal" -FilePath [path to json file] -PublishAd
```
3) Command Server - Import the certificate authority in Keyfactor Portal 

***
### Meta Data Fix Patch for Version 1.0.9 Steps
1) Stop the CSC Global Gateway Service
2) Run the following SQL In your CSC Global Gateway Database <br/>

```Delete Certificates WHERE LEN("CARequestId") <> 36```

3) Copy the New CSCGlobal v1.0.9 or later Binaries to the Gateway Directory Typically “c:\Progam Files\Keyfactor\Keyfactor AnyGateway” on the Gateway Server
4) Start the Gateway service and wait for the next sync between the GW Database and Keyfactor

### License
[Apache](https://apache.org/licenses/LICENSE-2.0)
