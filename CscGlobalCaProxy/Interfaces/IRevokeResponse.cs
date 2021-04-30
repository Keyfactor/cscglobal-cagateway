namespace Keyfactor.AnyGateway.CscGlobal.Interfaces
{
    public interface IRevokeResponse
    {
        string CommonName { get; set; }
        string CertificateType { get; set; }
        string Status { get; set; }
    }
}