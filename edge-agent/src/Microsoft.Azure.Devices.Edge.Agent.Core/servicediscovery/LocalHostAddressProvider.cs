namespace Microsoft.Azure.Devices.Edge.Agent.Core.ServiceDiscovery
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Threading.Tasks;

    public class LocalNetworkHostAddressProvider : IHostAddressProvider
    {
        public event EventHandler NetworkAddressChanged;

        public LocalNetworkHostAddressProvider()
        {
            NetworkChange.NetworkAddressChanged += (o, args) => this.NetworkAddressChanged?.Invoke(o, args);
        }
        

        public Task<IList<IPAddress>> GetAddress()
        {
            IList<IPAddress> addresses = new List<IPAddress>();
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var networkInterface in networkInterfaces)
            {
                if (networkInterface.OperationalStatus == OperationalStatus.Up && networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback
                    && networkInterface.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                {
                    foreach (var unicastIPAddressInformation in networkInterface.GetIPProperties().UnicastAddresses)
                    {
                        //TODO: filter IPv4 only
                        addresses.Add(unicastIPAddressInformation.Address);
                    }
                }
            }
           
            return Task.FromResult(addresses);
        }
    }
}
