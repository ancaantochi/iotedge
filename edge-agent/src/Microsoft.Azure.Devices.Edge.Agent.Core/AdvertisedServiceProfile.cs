// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Agent.Core
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    public class AdvertisedServiceProfile
    {
        [JsonConstructor]
        public AdvertisedServiceProfile(string serviceName, int ttl, ushort port, IDictionary<string, string> additionalInfo)
        {
            this.ServiceName = serviceName;
            this.Ttl = ttl;
            this.AdditionalInfo = additionalInfo;
            this.Port = port;
        }

        [JsonProperty(Required = Required.Always, PropertyName = "serviceName")]
        public string ServiceName { get;}

        [JsonProperty(PropertyName = "port")]
        public ushort Port { get; }

        [JsonProperty(Required = Required.Always, PropertyName = "ttl")]
        public int Ttl { get; }

        [JsonProperty(PropertyName = "additionalInfo")]
        public IDictionary<string, string> AdditionalInfo { get; }
    }
}
