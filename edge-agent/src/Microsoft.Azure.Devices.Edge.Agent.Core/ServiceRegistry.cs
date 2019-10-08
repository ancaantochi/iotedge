namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Net;
    using Makaretu.Dns;

    public class ServiceRegistry : IServiceRegistry
    {
        readonly string iothubHostname;
        readonly string deviceId;
        readonly string deviceHostname;
        readonly ServiceDiscovery serviceDiscovery;

        public ServiceRegistry(string deviceHostname, string iothubHostname, string deviceId)
        {
            this.serviceDiscovery = new ServiceDiscovery();
            this.deviceHostname = deviceHostname;
            this.iothubHostname = iothubHostname;
            this.deviceId = deviceId;
        }

        public bool AddService(ServiceInfo service)
        {
            ServiceProfile serviceProfile = this.ContructServiceProfile(service.InstanceName, service);
            this.serviceDiscovery.Advertise(serviceProfile);
            return true;
        }

        public bool RemoveService(ServiceInfo service)
        {
            ServiceProfile serviceProfile = this.ContructServiceProfile(service.InstanceName, service);
            this.serviceDiscovery.Unadvertise(serviceProfile);
            return true;
        }

        public void Start()
        {
            //foreach (KeyValuePair<string, ServiceInfo> service in this.services)
            //{
            //    var serviceProfile = this.ContructServiceProfile(service.Key, service.Value);

            //    this.serviceDiscovery.Advertise(serviceProfile);
            //}
        }

        ServiceProfile ContructServiceProfile(string instanceName, ServiceInfo service)
        {
            var serviceProfile = new ServiceProfile
            {
                InstanceName = instanceName,
                ServiceName = service.InstanceName
            };

            var fqn = serviceProfile.FullyQualifiedName;

            serviceProfile.HostName = this.deviceHostname;
            serviceProfile.Resources.Add(
                new SRVRecord
                {
                    Name = fqn,
                    Target = serviceProfile.HostName,
                    Port = service.Port
                });
            var txtRecord = new TXTRecord
            {
                Name = fqn,
                Strings = { "txtvers=1" }
            };

            //Add IotHub and deviceid
            txtRecord.Strings.Add($"iothubHostname={this.iothubHostname}");
            txtRecord.Strings.Add($"deviceId={this.deviceId}");

            if (service.Metadata != null)
            {
                foreach (KeyValuePair<string, string> info in service.Metadata)
                {
                    txtRecord.Strings.Add($"{info.Key}={info.Value}");
                }
            }

            serviceProfile.Resources.Add(txtRecord);

            //TODO: get IP address from edgelet
            string hostIps = Environment.GetEnvironmentVariable("HostIps") ?? string.Empty;
            string[] hostIpsStrings = hostIps.Split(' ');
            foreach (var address in hostIpsStrings)
            {
                if (IPAddress.TryParse(address, out IPAddress ipAddress))
                {
                    serviceProfile.Resources.Add(AddressRecord.Create(serviceProfile.HostName, ipAddress));
                }
            }

            return serviceProfile;
        }

        public void Stop()
        {
            this.serviceDiscovery.Dispose();
        }

    }
}
