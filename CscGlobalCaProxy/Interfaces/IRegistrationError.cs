namespace Keyfactor.AnyGateway.CscGlobal.Interfaces
{
    internal interface IRegistrationError
    {
        string Code { get; set; }
        string Description { get; set; }
        string Value { get; set; }
    }
}