namespace Keyfactor.AnyGateway.CscGlobal.Interfaces
{
    public interface IPrice
    {
        string Currency { get; set; }
        int Total { get; set; }
    }
}