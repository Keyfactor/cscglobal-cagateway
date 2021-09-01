namespace Keyfactor.AnyGateway.CscGlobal.Interfaces
{
    public interface IEvCertificateDetails
    {
        string Country { get; set; }
        string City { get; set; }
        string State { get; set; }
        string DateOfIncorporation { get; set; }
        string DoingBusinessAs { get; set; }
        string BusinessCategory { get; set; }
    }
}