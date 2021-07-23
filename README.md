<!-- add integration specific information below -->
*** 
# Getting Started
## Standard Gateway Installation
To begin, you must have the CA Gateway Service 21.3.2 installed and operational before attempting to configure the CSC Global plugin. This integration was tested with Keyfactor 8.7.0.0.
To install the gateway follow these instructions.

1) Gateway Server - run the installation .msi located [Here](https://github.com/Keyfactor/CSCGlobal-AnyGateway/raw/main/AnyGateway-21.3.2.msi)

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

1) Get the Latest Zip File from [Here](https://github.com/Keyfactor/CscGlobal-AnyGateway/releases)
2) Gateway Server - Copy the CscGlobalCaProxy.dll to the location where the Gateway Framework was installed (usually C:\Program Files\Keyfactor\Keyfactor AnyGateway)

### Configuration Changes
1) Gateway Server - Edit the CAProxyServer.exe.config file and replace the line that says "NoOp" with the line below:
   ```
   <alias alias="CAConnector" type="Keyfactor.AnyGateway.CscGlobal.CscGlobalCaProxy, CscGlobalCaProxy"/>
   ```
2) Gateway Server - Install the Root CSC Global Certificate that was received from CSC Global [Here](https://github.com/Keyfactor/CSCGlobal-AnyGateway/raw/main/AAACertificateServices.crt)

3) Gateway Server - Install the Intermediate CSC Global Certificate that was received from CSC Global [Here](https://github.com/Keyfactor/CSCGlobal-AnyGateway/raw/main/TrustedSecureCertificateAuthority5.crt)

4) Gateway Server - Take the sample Config.json located [Here](https://github.com/Keyfactor/CSCGlobal-AnyGateway/raw/main/SampleConfig.json) and make the following modifications

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
    "FromEmailAddress":"noReply@keyfactor.com",
    "CscGlobalEmail":"ServiceNowEmail@ServiceNow.com",
    "KeyfactorApiUserId":"SomeUserForKFAPI",
    "KeyfactorApiPassword":"SomePasswordForKFApi",
    "KeyfactorApiUrl":"https://kftrain.keyfactor.lab/KeyfactorAPI",
    "SmtpEmailHost":"smtp.mailgun.org",
    "EmailUserId":"SomeSTMPServiceUserId",
    "EmailPassword":"SomeSMTPServicePassword",
    "EmailPort":"587",
    "TemplateSync": "On"
  }
```

- *Template Settings*
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

1) Command Server - Copy and Unzip the Template Setup Files located [Here](https://github.com/Keyfactor/CSCGlobal-AnyGateway/raw/main/TemplateSetup.zip)
2) Command Server - Change the Security Settings in the CaTemplateUserSecurity.csv file to the appropriate settings for Test or Production
3) Command Server - Run the CreateTemplate.ps1 file and choose option 1 to create the templates in active directory.
   *Note if you get errors the security is likely wrong and you will have to add the security manually according to Keyfactor standards* 
4) Command Server - Use the Keyfactor Portal to Import the Templates created in Active Directory in step #3 above
5) Command Server - Run the CreateTemplate.ps1 file and choose option 3 to create all the enrollment fields.  
   *Note You will have to override the default API Questions to the appropriate information.*

### Certificate Authority Installation
1) Gateway Server - Start the Keyfactor Gateway Service
2) Run the set Gateway command similar to below
```ps
Set-KeyfactorGatewayConfig -LogicalName "CSCGlobal" -FilePath [path to json file] -PublishAd
```
3) Command Server - Import the certificate authority in Keyfactor Portal 

***

### License
[Apache](https://apache.org/licenses/LICENSE-2.0)
