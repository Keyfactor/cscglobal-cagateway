*** 
# Getting Started
## Standard Gateway Installation
To begin, you must have the CA Gateway Service 21.3.2 installed and operational before attempting to configure the CSC Global plugin. This integration was tested with Keyfactor 8.7.0.0.
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
    "TemplateSync": "On"
  }
```

- *Template Settings*
- For template settings you can either hard code them in the template parameters as shown on the last template or make them show up as enrollment parameters.  You can also have a combination of both enrollment parameters and hard coded parameters in the template parameters.  You can also build a workflow in Keyfactor to change them during enrollment based on some parameters as shown in the attached workflow 1.
```
  "Templates": {
    "CSC TrustedSecure Premium Certificate": {
      "ProductID": "CSC TrustedSecure Premium Certificate",
      "Parameters": {}
    },
    "CSC TrustedSecure EV Certificate": {
      "ProductID": "CSC TrustedSecure EV Certificate",
      "Parameters": {}
    },
    "CSC TrustedSecure UC Certificate": {
      "ProductID": "CSC TrustedSecure UC Certificate",
      "Parameters": {}
    },
    "CSC TrustedSecure Premium Wildcard Certificate": {
      "ProductID": "CSC TrustedSecure Premium Wildcard Certificate",
      "Parameters": {}
    },
    "CSC TrustedSecure Domain Validated SSL": {
      "ProductID": "CSC TrustedSecure Domain Validated SSL",
      "Parameters": {}
    },
    "CSC TrustedSecure Domain Validated Wildcard SSL": {
      "ProductID": "CSC TrustedSecure Domain Validated Wildcard SSL",
      "Parameters": {}
    },
    "CSC TrustedSecure Domain Validated UC Certificate": {
      "ProductID": "CSC TrustedSecure Domain Validated UC Certificate",
	"Parameters": {
		"Term": "12",
		"Applicant First Name": "Joe",
		"Applicant Last Name": "Smiht",
		"Applicant Email Address": "admin@jsmith.com",
		"Applicant Phone (+nn.nnnnnnnn)": "+12.34567890",
		"Domain Control Validation Method": "EMAIL",
		"Organization Contact": "Some Contact",
		"Business Unit": "Some Business Unit",
		"Notification Email(s) Comma Separated": "admin@jsmith.com",
		"CN DCV Email (admin@yourdomain.com)": "admin@jsmith.com",
		"Addtl Sans Comma Separated DVC Emails": "admin@jsmith.com"
	}
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

**PLEASE NOTE, AT THIS TIME THE RAPID_SSL TEMPLATE IS NOT SUPPORTED BY THE CSC API AND WILL NOT WORK WITH THIS INTEGRATION**

1) **Create ADFS Certificate Templates for the Following Products**
- CSC TrustedSecure Premium Certificate
- CSC TrustedSecure EV Certificate
- CSC TrustedSecure UC Certificate
- CSC TrustedSecure Premium Wildcard Certificate

2) **Import Into Keyfactor using the template import functionality**

3) **Edit each template and modify the Details and Enrollment Fields as Follows**
	
*CSC TrustedSecure UC Certificate - Details Tab*

CONFIG ELEMENT				| DESCRIPTION
----------------------------|------------------
Template Short Name	| CSC TrustedSecure Premium Certificate
Template Display Name	| CSC TrustedSecure Premium Certificate
Friendly Name	| CSC TrustedSecure Premium Certificate
Keys Size  | 2048
Enforce RFC 2818 Compliance | True
CSR Enrollment | True
Pfx Enrollment | True


*CSC TrustedSecure UC Certificate - Enrollment Fields*

NAME | DATA TYPE	| VALUES
-----|--------------|-----------------
Term | Multiple Choice | 12,24
Applicant First Name | String | N/A
Applicant Last Name | String | N/A
Applicant Email Address | String | N/A
Applicant Phone (+nn.nnnnnnnn) | String | N/A
Domain Control Validation Method | Multiple Choice | EMAIL
Organization Contact | Multiple Choice | Get From CSC Differs For Clients
Business Unit | Multiple Choice | Get From CSC Differs For Clients
Notification Email(s) Comma Separated | String | N/A
CN DCV Email (admin@yourdomain.com) | String | N/A
Addtl Sans Comma Separated DVC Emails | String | N/A
	
*CSC TrustedSecure EV Certificate - Details Tab*

CONFIG ELEMENT				| DESCRIPTION
----------------------------|------------------
Template Short Name	| CSC TrustedSecure EV Certificate
Template Display Name	| CSC TrustedSecure EV Certificate
Friendly Name	| CSC TrustedSecure EV Certificate
Keys Size  | 2048
Enforce RFC 2818 Compliance | True
CSR Enrollment | True
Pfx Enrollment | True


*CSC TrustedSecure EV Certificate - Enrollment Fields*

NAME | DATA TYPE	| VALUES
-----|--------------|-----------------
Term | Multiple Choice | 12,24
Applicant First Name | String | N/A
Applicant Last Name | String | N/A
Applicant Email Address | String | N/A
Applicant Phone (+nn.nnnnnnnn) | String | N/A
Domain Control Validation Method | Multiple Choice | EMAIL
Organization Contact | Multiple Choice | Get From CSC Differs For Clients
Business Unit | Multiple Choice | Get From CSC Differs For Clients
Notification Email(s) Comma Separated | String | N/A
CN DCV Email (admin@yourdomain.com) | String | N/A
Organization Country | String | N/A

*CSC TrustedSecure Premium Certificate - Details Tab*

CONFIG ELEMENT				| DESCRIPTION
----------------------------|------------------
Template Short Name	| CSC TrustedSecure Premium Certificate
Template Display Name	| CSC TrustedSecure Premium Certificate
Friendly Name	| CSC TrustedSecure Premium Certificate
Keys Size  | 2048
Enforce RFC 2818 Compliance | True
CSR Enrollment | True
Pfx Enrollment | True


*CSC TrustedSecure Premium Certificate - Enrollment Fields*

NAME | DATA TYPE	| VALUES
-----|--------------|-----------------
Term | Multiple Choice | 12,24
Applicant First Name | String | N/A
Applicant Last Name | String | N/A
Applicant Email Address | String | N/A
Applicant Phone (+nn.nnnnnnnn) | String | N/A
Domain Control Validation Method | Multiple Choice | EMAIL
Organization Contact | Multiple Choice | Get From CSC Differs For Clients
Business Unit | Multiple Choice | Get From CSC Differs For Clients
Notification Email(s) Comma Separated | String | N/A
CN DCV Email (admin@yourdomain.com) | String | N/A

*CSC TrustedSecure Premium Wildcard Certificate - Details Tab*

CONFIG ELEMENT				| DESCRIPTION
----------------------------|------------------
Template Short Name	| CSC TrustedSecure Premium Wildcard Certificate
Template Display Name	| CSC TrustedSecure Premium Wildcard Certificate
Friendly Name	| CSC TrustedSecure Premium Wildcard Certificate
Keys Size  | 2048
Enforce RFC 2818 Compliance | True
CSR Enrollment | True
Pfx Enrollment | True


*CSC TrustedSecure Premium Wildcard Certificate - Enrollment Fields*

NAME | DATA TYPE	| VALUES
-----|--------------|-----------------
Term | Multiple Choice | 12,24
Applicant First Name | String | N/A
Applicant Last Name | String | N/A
Applicant Email Address | String | N/A
Applicant Phone (+nn.nnnnnnnn) | String | N/A
Domain Control Validation Method | Multiple Choice | EMAIL
Organization Contact | Multiple Choice | Get From CSC Differs For Clients
Business Unit | Multiple Choice | Get From CSC Differs For Clients
Notification Email(s) Comma Separated | String | N/A
CN DCV Email (admin@yourdomain.com) | String | N/A


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
