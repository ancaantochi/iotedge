namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;

    public interface IHostAddressProvider
    {
        Task<IList<IPAddress>> GetAddress();
    }
}
