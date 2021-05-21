# {{ name }}
## {{ integration_type | capitalize }}

{{ description }}

<!-- add integration specific information below -->
*** 
# Getting Started
##Prerequsites
To begin, you must have the CA Gateway Service installed and operational before attempting to configure the CSC Global plugin. Follow the installation instructions
[here]().

##Configuration
It is important to note that importing the  Csc Global configuration into the CA Gateway prior to installing the binaries must be completed. Additionally, the CA Gateway service
must be running in order to succesfully import the configuation. When the CA Gateway service starts it will attempt to validate the connection information to 
the CA.  Without the imported configuration, the service will fail to start. 

The below example configuration can be modified and saved to the CA Gateway server and imported to suite a customer's needs.

```json
{
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
  },
  "CAConnection": {
    "CscGlobalURL": "https://apis-ote.cscglobal.com/dbs/api/v2",
    "ApiKey": "l7xx80452428423f43698b5718cd7081ca1c",
    "BearerToken": "44f20e43-f504-402a-b0e5-c30badaa6a29",
    "TemplateSync": "On"
  },
  "Templates": {
    "CscGlobal-Premium": {
      "ProductID": "CscGlobal-Premium",
      "Parameters": {}
    },
    "CscGlobal-EV": {
      "ProductID": "CscGlobal-EV",
      "Parameters": {}
    },
    "CscGlobal-UCC": {
      "ProductID": "CscGlobal-UCC",
      "Parameters": {}
    },
    "CscGlobal-Wildcard": {
      "ProductID": "CscGlobal-Wildcard",
      "Parameters": {}
    }
  },
  "CertificateManagers": null,
  "GatewayRegistration": {
    "LogicalName": "CscGlobal",
    "GatewayCertificate": {
      "StoreName": "CA",
      "StoreLocation": "LocalMachine",
      "Thumbprint": "d1eb23a46d17d68fd92564c2f1f1601764d8e349"
    }
  },
  "ServiceSettings": {
    "ViewIdleMinutes": 1,
    "FullScanPeriodHours": 1,
    "PartialScanPeriodMinutes": 1
  }
}
```

##Install
Once the CA Gateway configuration has been imported, the binaries need to be placed in the Keyfactor CA Gateway Service install directory 
(C:\\Program Files\\Keyfactor\\Keyfactor CA Gateway by default). These files can be found in the offical release build artifacts in Github.

***

### License
[Apache](https://apache.org/licenses/LICENSE-2.0)