namespace Keyfactor.AnyGateway.CscGlobal.Interfaces
{
    public interface IStatus
    {
        string Code { get; set; }
        string Message { get; set; }
        string AdditionalInformation { get; set; }
        string Uuid { get; set; }
    }
}