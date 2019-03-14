using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using DotNetty.Codecs.Mqtt.Packets;
using Makaretu.Dns;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using Microsoft.Azure.Devices.Provisioning.Client;
using Microsoft.Azure.Devices.Registration.Clients.Devices.V20190115;
using Microsoft.Azure.Devices.Registration.Clients.Devices.V20190115.Models;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Rest;
using Newtonsoft.Json;
using Zeroconf;
using Message = Microsoft.Azure.Devices.Client.Message;
using ServiceProfile = Microsoft.Azure.Devices.Client.ServiceProfile;

namespace poc
{
    class Program
    {
        static void Main(string[] args)
        {
            var tcs = new TaskCompletionSource<bool>();
            string advertise = Environment.GetEnvironmentVariable("Advertise");

            if (Boolean.TryParse(advertise, out bool adv))
            {
                Console.WriteLine("Advertise");
                Advertise();
            }
            else
            {
                //Console.WriteLine("Discover");
                Discover(tcs.Task);
            }
            
            tcs.Task.Wait();
            Console.ReadLine();
        }

        private static void Discover(Task<bool> task)
        {

            //var r = ZeroconfResolver.BrowseDomainsAsync().Result;
            //foreach (var r1 in r)
            //{
            //    Console.WriteLine($"{r1.Key}");
            //}

            IList<IPEndPoint> ips = new List<IPEndPoint>();
            
            bool.TryParse(Environment.GetEnvironmentVariable("USE_DPS_PROXY_DISCOVERY"), out bool useDpsProxy);
            if (useDpsProxy)
            {
                string dpsServiceName = "_dpsproxy._tcp.local.";
                Console.WriteLine($"Discovering Local DPS Proxy by service name {dpsServiceName}... \n");
                
                var zeroconfHosts = ZeroconfResolver.ResolveAsync(dpsServiceName).Result;
                foreach (var zeroConfHost in zeroconfHosts)
                {
                    if (zeroConfHost.DisplayName.Equals("dpsProxy", StringComparison.CurrentCultureIgnoreCase))
                    {
                        foreach (var ipAddress in zeroConfHost.IPAddresses)
                        {
                            if (ipAddress.StartsWith("10"))
                            {
                                Console.WriteLine(
                                    $"Discovered Local DPS Proxy \n  IP {ipAddress} \n  PORT {zeroConfHost.Services[dpsServiceName].Port} \n");
                                ips.Add(
                                    (new IPEndPoint(IPAddress.Parse(ipAddress),
                                        zeroConfHost.Services[dpsServiceName].Port)));
                            }
                        }

                    }
                }
            }
            else
            {
                string proxyIp = Environment.GetEnvironmentVariable("PROXY_IP");
                if (!string.IsNullOrEmpty(proxyIp))
                {
                    int proxyPort = int.Parse(Environment.GetEnvironmentVariable("PROXY_PORT"));
                    ips.Add(new IPEndPoint(IPAddress.Parse(proxyIp), proxyPort));
                    Console.WriteLine($"Proxy ip {proxyIp} \n");
                }
                else
                {
                    Console.WriteLine($"Provisioning without Local DPS Proxy \n");
                }
            }

            string secondaryKey = Environment.GetEnvironmentVariable("SECONDARY_KEY");
            string globalDeviceEndpoint = "global.azure-devices-provisioning.net";
            string idScope = "0NE76987D37";//Environment.GetEnvironmentVariable("ID_SCOPE");
            string registrationId = Environment.GetEnvironmentVariable("REGISTARTION_ID");
            string primaryKey = Environment.GetEnvironmentVariable("PRIMARY_KEY");
            //var transport = new ProvisioningTransportHandlerHttp();
            //if (ips.Count > 0)
            //{
            //    transport.Proxy = new WebProxy($"http://{ips[0].Address}:{ips[0].Port}");
            //}

            Console.WriteLine($"Provisioning using DPS Configuration: \n  Global endpoint: {globalDeviceEndpoint} \n  ID Scope: {idScope} \n  Registration Id: {registrationId}");
            Console.WriteLine();

            var handler = new HttpClientHandler();
            if (ips.Count > 0)
            {
                handler.UseProxy = true;
                handler.Proxy = new WebProxy($"http://{ips[0].Address}:{ips[0].Port}");
            }

            var dpsClient = new ProvisioningDeviceClient(new SymmetricKeyCredentials(primaryKey), handler);
            var registration = new RuntimeRegistration(dpsClient);
            //var dpsClient = ProvisioningDeviceClient.Create(globalDeviceEndpoint, idScope,
            //        new SecurityProviderSymmetricKey(registrationId, primaryKey, secondaryKey), transport);
            
            //var result = dpsClient.RegisterAsync().Result;

            var resultr = registration.RegisterDeviceWithHttpMessagesAsync(registrationId, new DeviceRegistration(registrationId), idScope).Result;

            Console.WriteLine("Successfully provisioned by DPS. \n");
            string serviceName = "Winterfell";
            //Console.WriteLine($"Received DPS registration info \n  DeviceId: {result.DeviceId}, \n  IoT Hub: {result.AssignedHub}, \n  Local Edge Gateway service name: {serviceName}  <=== ***This is new configuration metadata from DPS*** \n");

            string iothub = Environment.GetEnvironmentVariable("IOTHUB"); //result.AssignedHub
            string deviceId = Environment.GetEnvironmentVariable("DEVICE_ID"); //result.DeviceId
            var serviceDiscovery = new MDnsServiceDiscovery("edgehub");
            //var t = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            //t.Proxy = new WebProxy($"http://{ips[0].Address}:{ips[0].Port}");
            var dc = DeviceClient.Create(serviceDiscovery, iothub, new DeviceAuthenticationWithRegistrySymmetricKey(deviceId, primaryKey),
                new ITransportSettings[] { new MqttTransportSettings(TransportType.Mqtt_Tcp_Only) });

            //var m =ModuleClient.CreateFromConnectionString("GatewayHostname=edge.iot.microsoft.com;HostName=ancan-edge.azure-devices.net;DeviceId=ancan-device6;ModuleId=tempFilterFunctions;SharedAccessKey=/Dt060kAFfC0+5Iq9HRxLuHxJ66v3hN2D2aNJyxnZOM=", new ITransportSettings[] { t });
            //m.OpenAsync().Wait();

            Console.WriteLine($"Discovering Local Edge Gateway by service name {serviceName}... \n");
            var edgeHub = serviceDiscovery.GetServiceProfileAsync().Result;
            Console.WriteLine($"Discovered Local Edge Gateway \n  Hostname: {edgeHub.Hostname} \n  IP: {edgeHub.Addresses[0].Address} \n");
            Console.WriteLine($"Connecting to {edgeHub.Hostname}");
            dc.OpenAsync().Wait();
            Console.WriteLine($"Connected to {edgeHub.Hostname}");
            Random rnd = new Random();
            int i = 0;
            string[] funnyMessages = new string[]
            {
                "Hi Azure IoT! I am Jon Snow Targaryen-Stark, I know it's complicated, but you can call me the King in the North.",
                "The irony that Whitewalkers use AWS Greengrass for their Edge needs is not lost on me.",
                "I want to start a POC on connected dragons. Where do I get started ? There are so many docs!",
                "People say I have a brooding look about me. Well that is just my resting face, deal with it.",
                "Dragons are waaaay easier than dealing with certificates."
                //"I probably, just may have, a slight chance of daddy issues.Maybe not, I am Jon Snow - The King in the North.",
                
            };
            while (!task.IsCanceled)
            {
                var msg = new MessageBody()
                {
                    Message = funnyMessages[i % funnyMessages.Length] + " : Temperature in Winterfell",
                    Temperature = rnd.NextDouble(),
                    TimeCreated = DateTime.UtcNow
                };
                string dataBuffer = JsonConvert.SerializeObject(msg);
                var eventMessage = new Message(Encoding.UTF8.GetBytes(dataBuffer));
                Console.WriteLine($"Sending message #{i} {dataBuffer}");
                dc.SendEventAsync(eventMessage).Wait();
                //Console.WriteLine($"Message# {i} sent.");
                i++;
                Task.Delay(TimeSpan.FromSeconds(5)).Wait();
            }
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

    public class MessageBody
    {
        [JsonProperty(PropertyName = "machine")]
        public string Message { get; set; }

        [JsonProperty(PropertyName = "ambient")]
        public double Temperature { get; set; }

        [JsonProperty(PropertyName = "timeCreated")]
        public DateTime TimeCreated { get; set; }
    }
}
