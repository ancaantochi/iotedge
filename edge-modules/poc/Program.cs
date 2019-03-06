using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using DotNetty.Codecs.Mqtt.Packets;
using Makaretu.Dns;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Provisioning.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Zeroconf;
using Message = Microsoft.Azure.Devices.Client.Message;
using ServiceProfile = Microsoft.Azure.Devices.Client.ServiceProfile;

namespace poc
{
    class Program
    {
        static void Main(string[] args)
        {
            string advertise = Environment.GetEnvironmentVariable("Advertise");

            if (Boolean.TryParse(advertise, out bool adv))
            {
                Console.WriteLine("Advertise");
                Advertise();
            }
            else
            {
                Console.WriteLine("Discover");
                Discover();
            }


            var tcs = new TaskCompletionSource<bool>();
            tcs.Task.Wait();
            Console.ReadLine();
        }

        private static void Discover()
        {

            //var r = ZeroconfResolver.BrowseDomainsAsync().Result;
            //foreach (var r1 in r)
            //{
            //    Console.WriteLine($"{r1.Key}");
            //}

            //IList<IPEndPoint> ips = new List<IPEndPoint>();
            //string serviceName = "_dpsproxy._tcp.local.";
            //var zeroconfHosts = ZeroconfResolver.ResolveAsync(serviceName).Result;
            //foreach (var zeroConfHost in zeroconfHosts)
            //{
            //    Console.WriteLine($"DisplayName {zeroConfHost.DisplayName}");
            //    if (zeroConfHost.DisplayName.Equals("dpsProxy", StringComparison.CurrentCultureIgnoreCase))
            //    {
            //        foreach (var ipAddress in zeroConfHost.IPAddresses)
            //        {
            //            Console.WriteLine($"IP {ipAddress} PORT {zeroConfHost.Services[serviceName].Port}");
            //            ips.Add((new IPEndPoint(IPAddress.Parse(ipAddress), zeroConfHost.Services[serviceName].Port)));
            //        }

            //    }
            //}

            //Console.WriteLine($"Discovered {ips.Count} services");

            //if (ips.Count > 0)
            //{
            //string secondaryKey = Environment.GetEnvironmentVariable("SECONDARY_KEY");
            //string globalDeviceEndpoint = "global.azure-devices-provisioning.net";
            //string idScope = Environment.GetEnvironmentVariable("ID_SCOPE");
            //string registrationId = Environment.GetEnvironmentVariable("REGISTARTION_ID");
            //string primaryKey = Environment.GetEnvironmentVariable("PRIMARY_KEY");
            //var transport = new ProvisioningTransportHandlerHttp();
            //transport.Proxy = new WebProxy(ips[0].Address.ToString(), ips[0].Port);
            //var dpsClient = ProvisioningDeviceClient.Create(globalDeviceEndpoint, idScope,
            //    new SecurityProviderSymmetricKey(registrationId, primaryKey, secondaryKey), transport);
            //var result = dpsClient.RegisterAsync().Result;

            //Console.WriteLine(result);

            string iothub = Environment.GetEnvironmentVariable("IOTHUB"); //result.AssignedHub
            string deviceId = Environment.GetEnvironmentVariable("DEVICE_ID"); //result.DeviceId
            string primaryKey = Environment.GetEnvironmentVariable("PRIMARY_KEY");
            var serviceDiscovery = new MDnsServiceDiscovery("edgehub");
            var dc = DeviceClient.Create(serviceDiscovery, iothub, new DeviceAuthenticationWithRegistrySymmetricKey(deviceId, primaryKey),
                new ITransportSettings[] { new MqttTransportSettings(TransportType.Mqtt_Tcp_Only) });

            string edgeHub = serviceDiscovery.GetServiceProfileAsync().Result.Hostname;
            Console.WriteLine($"Calling DeviceClient Open Iothub {iothub} deviceId {deviceId} edgeHub {edgeHub}");

            dc.OpenAsync().Wait();
            Console.WriteLine("Connected");

            dc.SendEventAsync(new Message()).Wait();
            Console.WriteLine("Message sent");

            //}
        }

        private static void Advertise()
        {
            var serviceProfile = new Makaretu.Dns.ServiceProfile
            {
                InstanceName = "dpsProxy",
                ServiceName = "_dpsproxy._tcp"
            };

            var fqn = serviceProfile.FullyQualifiedName;

            //TODO: get hostname from env EdgeDeviceHostName 
            serviceProfile.HostName = "dpsProxy";
            serviceProfile.Resources.Add(new SRVRecord
            {
                Name = fqn,
                Target = serviceProfile.HostName,
                Port = 8989
            });
            var txtRecord = new TXTRecord
            {
                Name = fqn,
                Strings = { "txtvers=1" }
            };

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

            var serviceProfile2 = new Makaretu.Dns.ServiceProfile("dpsProxy", "_dpsproxy._tcp", 4343);
            var sd = new ServiceDiscovery();
            sd.Advertise(serviceProfile2);
        }


        public static IEnumerable<NetworkInterface> GetNetworkInterfaces()
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
                .Where(nic => nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToArray();


            return nics;
        }


        public static IEnumerable<IPAddress> GetIPAddresses()
        {
            return GetNetworkInterfaces()
                .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
                .Select(u => u.Address);
        }

        public static IEnumerable<IPAddress> GetLinkLocalAddresses()
        {
            return GetIPAddresses()
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork);
        }
    }
}
