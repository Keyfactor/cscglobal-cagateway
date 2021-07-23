using System.Collections.Generic;

namespace Keyfactor.AnyGateway.CscGlobal.Interfaces
{
    public interface INotifications
    {
        bool Enabled { get; set; }
        List<string> AdditionalNotificationEmails { get; set; }
    }
}