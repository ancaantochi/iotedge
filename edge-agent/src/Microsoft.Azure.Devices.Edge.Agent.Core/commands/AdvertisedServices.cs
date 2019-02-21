// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.Planners
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Makaretu.Dns;

    class AdvertisedServices : ICommand
    {
        IDictionary<string, AdvertisedServiceProfile> serviceProfiles;

        public AdvertisedServices(IDictionary<string, AdvertisedServiceProfile> serviceProfiles)
        {
            this.serviceProfiles = serviceProfiles;
        }

        public string Show() => "Advertise";

        public string Id { get; } = "Advertise";

        public Task ExecuteAsync(CancellationToken token)
        {
            foreach (KeyValuePair<string, AdvertisedServiceProfile> service in this.serviceProfiles)
            {
                var serviceProfile = new ServiceProfile
                {
                    InstanceName = service.Key,
                    ServiceName = service.Value.ServiceName
                };

                var fqn = serviceProfile.FullyQualifiedName;

                //TODO: get hostname from env EdgeDeviceHostName 
                serviceProfile.HostName = "edgehub.local";
                serviceProfile.Resources.Add(new SRVRecord
                {
                    Name = fqn,
                    Target = serviceProfile.HostName,
                    Port = service.Value.Port
                });
                var txtRecord = new TXTRecord
                {
                    Name = fqn,
                    Strings = { "txtvers=1" }
                };
                if (service.Value.AdditionalInfo != null)
                {
                    foreach (KeyValuePair<string, string> info in service.Value.AdditionalInfo)
                    {
                        txtRecord.Strings.Add($"{info.Key}={info.Value}");
                    }
                }

                serviceProfile.Resources.Add(txtRecord);

                //TODO: get IP address from edgelet
                string hostIps = Environment.GetEnvironmentVariable("HostIps") ?? String.Empty;
                string[] hostIpsStrings = hostIps.Split(' ');
                foreach (var address in hostIpsStrings)
                {
                    if (IPAddress.TryParse(address, out IPAddress ipAddress))
                    {
                        serviceProfile.Resources.Add(AddressRecord.Create(serviceProfile.HostName, ipAddress));
                    }
                }

                var sd = new ServiceDiscovery();
                sd.Advertise(serviceProfile);
            }
            return Task.CompletedTask;
        }

        public Task UndoAsync(CancellationToken token) => throw new System.NotImplementedException();
    }
}
