﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using CAProxy.AnyGateway;
using CAProxy.AnyGateway.Interfaces;
using CAProxy.AnyGateway.Models;
using CAProxy.Common;
using CSS.PKI;

namespace Keyfactor.AnyGateway.CscGlobal
{
    public class CscGlobalCaProxy:BaseCAConnector
    {
        public override int Revoke(string caRequestId, string hexSerialNumber, uint revocationReason)
        {
            throw new NotImplementedException();
        }

        [Obsolete]
        public override void Synchronize(ICertificateDataReader certificateDataReader, BlockingCollection<CertificateRecord> blockingBuffer,
            CertificateAuthoritySyncInfo certificateAuthoritySyncInfo, CancellationToken cancelToken, string logicalName)
        {
            throw new NotImplementedException();
        }

        [Obsolete]
        public override EnrollmentResult Enroll(string csr, string subject, Dictionary<string, string[]> san, EnrollmentProductInfo productInfo,
            PKIConstants.X509.RequestFormat requestFormat, RequestUtilities.EnrollmentType enrollmentType)
        {
            throw new NotImplementedException();
        }

        public override CAConnectorCertificate GetSingleRecord(string caRequestId)
        {
            throw new NotImplementedException();
        }

        public override void Initialize(ICAConnectorConfigProvider configProvider)
        {
            throw new NotImplementedException();
        }

        public override void Ping()
        {
            throw new NotImplementedException();
        }

        public override void ValidateCAConnectionInfo(Dictionary<string, object> connectionInfo)
        {
            throw new NotImplementedException();
        }

        public override void ValidateProductInfo(EnrollmentProductInfo productInfo, Dictionary<string, object> connectionInfo)
        {
            throw new NotImplementedException();
        }
    }
}
