// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core.ServiceDiscovery
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Makaretu.Dns;
    using Microsoft.Azure.Devices.Edge.Util.Concurrency;

    public class ServiceRegistry : IServiceRegistry
    {
        ServiceDiscovery serviceDiscovery;
        readonly AsyncLock serviceLock = new AsyncLock();
        readonly string iothubHostname;
        readonly string deviceId;
        readonly string deviceHostname;
        readonly ConcurrentDictionary<string, ServiceInfo> services;
        readonly IHostAddressProvider hostAddressProvider;

        public ServiceRegistry(string deviceHostname, string iothubHostname, string deviceId, IHostAddressProvider hostAddressProvider)
        {
            //this.serviceDiscovery = new ServiceDiscovery();
            this.services = new ConcurrentDictionary<string, ServiceInfo>();
            this.deviceHostname = deviceHostname;
            this.iothubHostname = iothubHostname;
            this.deviceId = deviceId;
            this.hostAddressProvider = hostAddressProvider;
        }

        public async Task<bool> AddService(string instanceName, ServiceInfo service)
        {
            await this.EnsureServiceDiscoveryIsStarted();

            ServiceProfile serviceProfile = await this.ContructServiceProfile(instanceName, service);
            this.services.AddOrUpdate(serviceProfile.FullyQualifiedName.ToString(), service, (s, info) => service);
            this.serviceDiscovery.Advertise(serviceProfile);
            return true;
        }

        public async Task<bool> UpdateService(string instanceName, ServiceInfo service)
        {
            ServiceProfile serviceProfile = await this.ContructServiceProfile(instanceName, service);
            this.services.AddOrUpdate(serviceProfile.FullyQualifiedName.ToString(), service, (s, info) => service);
            this.serviceDiscovery.Unadvertise(serviceProfile);
            this.serviceDiscovery.Advertise(serviceProfile);
            return true;
        }

        public async Task<bool> RemoveService(string instanceName, ServiceInfo service)
        {
            ServiceProfile serviceProfile = await this.ContructServiceProfile(instanceName, service);
            this.services.TryRemove(serviceProfile.FullyQualifiedName.ToString(), out ServiceInfo _);
            this.serviceDiscovery.Unadvertise(serviceProfile);

            await this.StopServiceDicovery();
            return true;
        }

        async Task EnsureServiceDiscoveryIsStarted()
        {
            if (this.serviceDiscovery == null)
            {
                using (await this.serviceLock.LockAsync())
                {
                    if (this.serviceDiscovery == null)
                    {
                        this.serviceDiscovery = new ServiceDiscovery();
                    }
                }
            }
        }

        async Task<ServiceProfile> ContructServiceProfile(string instanceName, ServiceInfo service)
        {
            var serviceProfile = new ServiceProfile
            {
                InstanceName = instanceName,
                ServiceName = service.ServiceName
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

            foreach (IPAddress address in await this.hostAddressProvider.GetAddress())
            {
                serviceProfile.Resources.Add(AddressRecord.Create(serviceProfile.HostName, address));
            }

            return serviceProfile;
        }

        async Task StopServiceDicovery()
        {
            if (this.services.IsEmpty)
            {
                using (await this.serviceLock.LockAsync())
                {
                    if (this.services.IsEmpty)
                    {
                        this.serviceDiscovery.Dispose();
                        this.serviceDiscovery = null;
                    }
                }
            }
        }
    }
}
