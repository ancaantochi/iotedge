namespace Microsoft.Azure.Devices.Edge.Agent.Core.ServiceDiscovery
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;

    public interface IHostAddressProvider
    {
        event EventHandler NetworkAddressChanged;

        Task<IList<IPAddress>> GetAddress();
    }
}
