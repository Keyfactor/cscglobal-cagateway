using System;

namespace Keyfactor.AnyGateway.CscGlobal.Exceptions
{
    public class RetryCountExceededException : Exception
    {
        public RetryCountExceededException(string message) : base(message)
        {
        }
    }
}