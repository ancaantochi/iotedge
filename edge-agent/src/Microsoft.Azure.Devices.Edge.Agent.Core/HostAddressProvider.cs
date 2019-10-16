namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Threading.Tasks;

    public class HostAddressProvider : IHostAddressProvider
    {
        public Task<IList<IPAddress>> GetAddress()
        {
            //TODO: get IP address from edgelet
            IList<IPAddress> addresses = new List<IPAddress>();
            string hostIps = Environment.GetEnvironmentVariable("HostIps") ?? string.Empty;
            string[] hostIpsStrings = hostIps.Split(' ');
            foreach (string address in hostIpsStrings)
            {
                if (IPAddress.TryParse(address, out IPAddress ipAddress))
                {
                    addresses.Add(ipAddress);
                }
            }

            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;

            return Task.FromResult(addresses);
        }

        private void NetworkChange_NetworkAddressChanged(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}
