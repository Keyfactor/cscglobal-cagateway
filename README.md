# Csc Global
## Ca-gateway

Csc Global operates a PKI as a service platform for customers around the globe.  The AnyGateway solution for CscGlobal is designed to allow Keyfactor Command the ability to: - Sync certificates issued from the CA - Request new certificates from the CA - Revoke certificates directly from Keyfactor Command

<!-- add integration specific information below -->
*** 
# Getting Started
## Prerequsites
To begin, you must have the CA Gateway Service installed and operational before attempting to configure the CSC Global plugin. Follow the installation instructions
[here]().

## Configuration
It is important to note that importing the  Csc Global configuration into the CA Gateway prior to installing the binaries must be completed. Additionally, the CA Gateway service
must be running in order to succesfully import the configuation. When the CA Gateway service starts it will attempt to validate the connection information to 
the CA.  Without the imported configuration, the service will fail to start.

Configuration Video [here](https://web.microsoftstream.com/video/b72590d2-9c11-41bf-be01-9cd36bdafd21?list=studio).

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

## Enrollment
There are 4 different templates used in enrollment.  There is an EV Template, Premium Template, UCC Template and Wildcard Template.  Below is a short video description of each
template and how they work with the enrollment process.

### CSC Global EV Template
[Video](https://web.microsoftstream.com/video/bb3db223-65ba-491d-bd8b-9d5d3fed98ff)

### CSC Global Premium Template
[Video](https://web.microsoftstream.com/video/0ac0c3fb-ff07-4b23-ba8c-78f1361c84ee)

### CSC Global Wildcard Template
[Video](https://web.microsoftstream.com/video/d5ff6d55-36d7-4df4-b46a-dd173db91006)

### CSC Global UCC (Multi-Domain) Template
[Video](https://web.microsoftstream.com/video/807d833c-b4b6-43d8-86cc-f47b04d0eb23)

## Install
Once the CA Gateway configuration has been imported, the binaries need to be placed in the Keyfactor CA Gateway Service install directory 
(C:\\Program Files\\Keyfactor\\Keyfactor CA Gateway by default). These files can be found in the offical release build artifacts in Github.

***

### License
[Apache](https://apache.org/licenses/LICENSE-2.0)
