namespace Keyfactor.AnyGateway.CscGlobal.Interfaces
{
    public interface IDomainControlValidation
    {
        string MethodType { get; set; }
        string EmailAddress { get; set; }
    }
}