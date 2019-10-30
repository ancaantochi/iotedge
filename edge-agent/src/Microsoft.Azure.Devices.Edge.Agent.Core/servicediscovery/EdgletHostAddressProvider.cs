namespace Microsoft.Azure.Devices.Edge.Agent.Core.ServiceDiscovery
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;

    public class EdgeletHostAddressProvider : IHostAddressProvider
    {
        public event EventHandler NetworkAddressChanged;

        public Task<IList<IPAddress>> GetAddress()
        {
            //TODO: get IP address from edgelet
            IList<IPAddress> addresses = new List<IPAddress>();
            

            return Task.FromResult(addresses);
        }

        void CheckNetworkChange()
        {
            if (NetworkAddressChanged != null)
            {
                NetworkAddressChanged(this, EventArgs.Empty);
            }
        }
    }
}
