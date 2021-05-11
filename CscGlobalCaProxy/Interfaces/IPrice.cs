namespace Keyfactor.AnyGateway.CscGlobal.Interfaces
{
    public interface IPrice
    {
        string Currency { get; set; }
        decimal Total { get; set; }
    }
}