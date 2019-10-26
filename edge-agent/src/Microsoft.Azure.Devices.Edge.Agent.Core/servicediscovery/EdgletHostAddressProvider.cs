namespace Microsoft.Azure.Devices.Edge.Agent.Core.ServiceDiscovery
{
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;

    public class EdgeletHostAddressProvider : IHostAddressProvider
    {
        public Task<IList<IPAddress>> GetAddress()
        {
            //TODO: get IP address from edgelet
            IList<IPAddress> addresses = new List<IPAddress>();
            

            return Task.FromResult(addresses);
        }
    }
}
