// Copyright (c) Microsoft. All rights reserved.
namespace Microsoft.Azure.Devices.Edge.Hub.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection.Metadata.Ecma335;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Edge.Hub.Core.Identity.Service;
    using Microsoft.Azure.Devices.Edge.Storage;
    using Microsoft.Azure.Devices.Edge.Util;
    using Microsoft.Azure.Devices.Edge.Util.Json;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Nito.AsyncEx;
    using AsyncLock = Microsoft.Azure.Devices.Edge.Util.Concurrency.AsyncLock;

    public sealed class DeviceScopeIdentitiesCache : IDeviceScopeIdentitiesCache
    {
        static readonly TimeSpan DefaultRefreshDelay = TimeSpan.FromMinutes(2);
        readonly IServiceProxy serviceProxy;
        readonly IKeyValueStore<string, string> encryptedStore;
        readonly AsyncLock cacheLock = new AsyncLock();
        readonly IDictionary<string, StoredServiceIdentity> serviceIdentityCache;
        readonly Timer refreshCacheTimer;
        readonly TimeSpan refreshRate;
        readonly TimeSpan refreshDelay;
        readonly AsyncAutoResetEvent refreshCacheSignal = new AsyncAutoResetEvent();
        readonly object refreshCacheLock = new object();

        Task refreshCacheTask;

        DeviceScopeIdentitiesCache(
            IServiceProxy serviceProxy,
            IKeyValueStore<string, string> encryptedStorage,
            IDictionary<string, StoredServiceIdentity> initialCache,
            TimeSpan refreshRate,
            TimeSpan refreshDelay)
        {
            this.serviceProxy = serviceProxy;
            this.encryptedStore = encryptedStorage;
            this.serviceIdentityCache = initialCache;
            this.refreshRate = refreshRate;
            this.refreshDelay = refreshDelay;
            this.refreshCacheTimer = new Timer(this.RefreshCache, null, TimeSpan.Zero, refreshRate);
        }

        public event EventHandler<string> ServiceIdentityRemoved;

        public event EventHandler<ServiceIdentity> ServiceIdentityUpdated;

        public static Task<DeviceScopeIdentitiesCache> Create(
            IServiceProxy serviceProxy,
            IKeyValueStore<string, string> encryptedStorage,
            TimeSpan refreshRate)
        {
            return Create(serviceProxy, encryptedStorage, refreshRate, DefaultRefreshDelay);
        }

        internal static async Task<DeviceScopeIdentitiesCache> Create(
            IServiceProxy serviceProxy,
            IKeyValueStore<string, string> encryptedStorage,
            TimeSpan refreshRate,
            TimeSpan refreshDelay)
        {
            Preconditions.CheckNotNull(serviceProxy, nameof(serviceProxy));
            Preconditions.CheckNotNull(encryptedStorage, nameof(encryptedStorage));
            IDictionary<string, StoredServiceIdentity> cache = await ReadCacheFromStore(encryptedStorage);
            var deviceScopeIdentitiesCache = new DeviceScopeIdentitiesCache(serviceProxy, encryptedStorage, cache, refreshRate, refreshDelay);
            Events.Created();
            return deviceScopeIdentitiesCache;
        }

        public void InitiateCacheRefresh()
        {
            Events.ReceivedRequestToRefreshCache();
            this.refreshCacheSignal.Set();
        }

        public async Task RefreshServiceIdentity(string id)
        {
            try
            {
                Events.RefreshingServiceIdentity(id);
                Option<ServiceIdentity> serviceIdentity = await this.GetServiceIdentityFromService(id);
                await serviceIdentity
                    .Map(s => this.HandleNewServiceIdentity(s))
                    .GetOrElse(() => this.HandleNoServiceIdentity(id));
            }
            catch (DeviceInvalidStateException)
            {
                await this.HandleNoServiceIdentity(id);
            }
            catch (Exception e)
            {
                Events.ErrorRefreshingCache(e, id);
            }
        }

        bool ShouldRefresh(StoredServiceIdentity storedServiceIdentity, bool refreshCachedIdentity) => refreshCachedIdentity && storedServiceIdentity.Timestamp + this.refreshDelay <= DateTime.UtcNow;

        void VerifyServiceIdentity(string id, StoredServiceIdentity storedServiceIdentity) => this.VerifyServiceIdentity(id, storedServiceIdentity.ServiceIdentity);

        void VerifyServiceIdentity(string id, Option<ServiceIdentity> serviceIdentity) => serviceIdentity.ForEach(
                si =>
                {
                    if (si.Status != ServiceIdentityStatus.Enabled)
                    {
                        Events.VerifyServiceIdentityFailure(id, "Device is disabled.");
                        throw new DeviceInvalidStateException("Device is disabled.");
                    }
                },
                () =>
                {
                    Events.VerifyServiceIdentityFailure(id, "Device is out of scope.");
                    throw new DeviceInvalidStateException("Device is out of scope.");
                });

        async Task RefreshServiceIdentityInternal(string id)
        {
            try
            {
                // start refresh
                Events.RefreshingServiceIdentity(id);
                // Successfully get response from server
                Option<ServiceIdentity> serviceIdentity = await this.GetServiceIdentityFromService(id);
                // If found device, update it otherwise something is wrong with the device, set it as invalid
                await serviceIdentity
                    .Map(s => this.HandleNewServiceIdentity(s))
                    .GetOrElse(() => this.HandleNoServiceIdentity(id));
            }
            catch (DeviceInvalidStateException ex)
            {
                Events.ErrorRefreshingCache(ex, id);
                // Device either out of scope or remove, set it as invalid
                await this.HandleNoServiceIdentity(id);
                throw;
            }
            catch (Exception e)
            {
                // Refresh failed
                Events.ErrorRefreshingCache(e, id);
                throw;
            }
        }

        public async Task VerifyServiceIdentityState(string id, bool refreshCachedIdentity = false)
        {
            Option<StoredServiceIdentity> storedServiceIdentity = await this.GetStoredServiceIdentity(id);

            var ssi = await storedServiceIdentity.Match(async (ssi) => {
                if (ShouldRefresh(ssi, refreshCachedIdentity))
                {
                    await this.RefreshServiceIdentity(id);
                    return await this.GetStoredServiceIdentity(id);
                }
                else return storedServiceIdentity;
            },
            async () =>
           {
               await this.RefreshServiceIdentity(id);
               return await this.GetStoredServiceIdentity(id);
           });

            this.VerifyServiceIdentity(id, ssi.Expect(() => {
                Events.VerifyServiceIdentityFailure(id, "Device is out of scope.");
                return new DeviceInvalidStateException("Device is out of scope.");
            }));
        }

        public async Task RefreshServiceIdentities(IEnumerable<string> ids)
        {
            List<string> idList = Preconditions.CheckNotNull(ids, nameof(ids)).ToList();
            foreach (string id in idList)
            {
                await this.RefreshServiceIdentity(id);
            }
        }

        public async Task<Option<ServiceIdentity>> GetServiceIdentity(string id, bool refreshIfNotExists = false)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            Events.GettingServiceIdentity(id);
            if (refreshIfNotExists && !this.serviceIdentityCache.ContainsKey(id))
            {
                await this.RefreshServiceIdentity(id);
            }

            return await this.GetServiceIdentityInternal(id);
        }

        public void Dispose()
        {
            this.encryptedStore?.Dispose();
            this.refreshCacheTimer?.Dispose();
            this.refreshCacheTask?.Dispose();
        }

        internal Task<Option<ServiceIdentity>> GetServiceIdentityFromService(string id)
        {
            // If it is a module id, it will have the format "deviceId/moduleId"
            string[] parts = id.Split('/');
            if (parts.Length == 2)
            {
                return this.serviceProxy.GetServiceIdentity(parts[0], parts[1]);
            }
            else
            {
                return this.serviceProxy.GetServiceIdentity(id);
            }
        }

        static async Task<IDictionary<string, StoredServiceIdentity>> ReadCacheFromStore(IKeyValueStore<string, string> encryptedStore)
        {
            IDictionary<string, StoredServiceIdentity> cache = new Dictionary<string, StoredServiceIdentity>();
            await encryptedStore.IterateBatch(
                int.MaxValue,
                (key, value) =>
                {
                    cache.Add(key, JsonConvert.DeserializeObject<StoredServiceIdentity>(value));
                    return Task.CompletedTask;
                });
            return cache;
        }

        void RefreshCache(object state)
        {
            lock (this.refreshCacheLock)
            {
                if (this.refreshCacheTask == null || this.refreshCacheTask.IsCompleted)
                {
                    Events.InitializingRefreshTask(this.refreshRate);
                    this.refreshCacheTask = this.RefreshCache();
                }
            }
        }

        async Task RefreshCache()
        {
            while (true)
            {
                try
                {
                    Events.StartingRefreshCycle();
                    var currentCacheIds = new List<string>();
                    IServiceIdentitiesIterator iterator = this.serviceProxy.GetServiceIdentitiesIterator();
                    while (iterator.HasNext)
                    {
                        IEnumerable<ServiceIdentity> batch = await iterator.GetNext();
                        foreach (ServiceIdentity serviceIdentity in batch)
                        {
                            try
                            {
                                await this.HandleNewServiceIdentity(serviceIdentity);
                                currentCacheIds.Add(serviceIdentity.Id);
                            }
                            catch (Exception e)
                            {
                                Events.ErrorProcessing(serviceIdentity, e);
                            }
                        }
                    }

                    // Diff and update
                    List<string> removedIds = this.serviceIdentityCache
                        .Where(kvp => kvp.Value.ServiceIdentity.HasValue)
                        .Select(kvp => kvp.Key)
                        .Except(currentCacheIds).ToList();
                    await Task.WhenAll(removedIds.Select(id => this.HandleNoServiceIdentity(id)));
                }
                catch (Exception e)
                {
                    Events.ErrorInRefreshCycle(e);
                }

                Events.DoneRefreshCycle(this.refreshRate);
                await this.IsReady();
            }
        }

        async Task IsReady()
        {
            Task refreshCacheSignalTask = this.refreshCacheSignal.WaitAsync();
            Task sleepTask = Task.Delay(this.refreshRate);
            Task task = await Task.WhenAny(refreshCacheSignalTask, sleepTask);
            if (task == refreshCacheSignalTask)
            {
                Events.RefreshSignalled();
            }
            else
            {
                Events.RefreshSleepCompleted();
            }
        }

        async Task<Option<ServiceIdentity>> GetServiceIdentityInternal(string id)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            using (await this.cacheLock.LockAsync())
            {
                return this.serviceIdentityCache.TryGetValue(id, out StoredServiceIdentity storedServiceIdentity)
                    ? storedServiceIdentity.ServiceIdentity
                    : Option.None<ServiceIdentity>();
            }
        }

        async Task<Option<StoredServiceIdentity>> GetStoredServiceIdentity(string id)
        {
            Preconditions.CheckNonWhiteSpace(id, nameof(id));
            using (await this.cacheLock.LockAsync())
            {
                return this.serviceIdentityCache.TryGetValue(id, out StoredServiceIdentity storedServiceIdentity)
                    ? Option.Some(storedServiceIdentity)
                    : Option.None<StoredServiceIdentity>();
            }
        }

        async Task HandleNoServiceIdentity(string id)
        {
            using (await this.cacheLock.LockAsync())
            {
                bool hasValidServiceIdentity = this.serviceIdentityCache.TryGetValue(id, out StoredServiceIdentity existingServiceIdentity)
                    ? existingServiceIdentity.ServiceIdentity.Filter(s => s.Status == ServiceIdentityStatus.Enabled).HasValue
                    : false;
                var storedServiceIdentity = new StoredServiceIdentity(id);
                this.serviceIdentityCache[id] = storedServiceIdentity;
                await this.SaveServiceIdentityToStore(id, storedServiceIdentity);
                Events.NotInScope(id);

                if (hasValidServiceIdentity)
                {
                    // Remove device if connected, if service identity existed and then was removed.
                    this.ServiceIdentityRemoved?.Invoke(this, id);
                }
            }
        }

        async Task HandleNewServiceIdentity(ServiceIdentity serviceIdentity)
        {
            using (await this.cacheLock.LockAsync())
            {
                bool hasUpdated = this.serviceIdentityCache.TryGetValue(serviceIdentity.Id, out StoredServiceIdentity currentStoredServiceIdentity)
                                  && currentStoredServiceIdentity.ServiceIdentity
                                      .Map(s => !s.Equals(serviceIdentity))
                                      .GetOrElse(false);
                var storedServiceIdentity = new StoredServiceIdentity(serviceIdentity);
                this.serviceIdentityCache[serviceIdentity.Id] = storedServiceIdentity;
                await this.SaveServiceIdentityToStore(serviceIdentity.Id, storedServiceIdentity);
                Events.AddInScope(serviceIdentity.Id);
                if (hasUpdated)
                {
                    this.ServiceIdentityUpdated?.Invoke(this, serviceIdentity);
                }
            }
        }

        async Task SaveServiceIdentityToStore(string id, StoredServiceIdentity storedServiceIdentity)
        {
            string serviceIdentityString = JsonConvert.SerializeObject(storedServiceIdentity);
            await this.encryptedStore.Put(id, serviceIdentityString);
        }

        internal class StoredServiceIdentity
        {
            public StoredServiceIdentity(ServiceIdentity serviceIdentity)
                : this(Preconditions.CheckNotNull(serviceIdentity, nameof(serviceIdentity)).Id, serviceIdentity, DateTime.UtcNow)
            {
            }

            public StoredServiceIdentity(string id)
                : this(Preconditions.CheckNotNull(id, nameof(id)), null, DateTime.UtcNow)
            {
            }

            [JsonConstructor]
            StoredServiceIdentity(string id, ServiceIdentity serviceIdentity, DateTime timestamp)
            {
                this.ServiceIdentity = Option.Maybe(serviceIdentity);
                this.Id = Preconditions.CheckNotNull(id);
                this.Timestamp = timestamp;
            }

            [JsonProperty("serviceIdentity")]
            [JsonConverter(typeof(OptionConverter<ServiceIdentity>))]
            public Option<ServiceIdentity> ServiceIdentity { get; }

            [JsonProperty("id")]
            public string Id { get; }

            [JsonProperty("timestamp")]
            public DateTime Timestamp { get; }
        }
    }

    static class Events
    {
        const int IdStart = HubCoreEventIds.DeviceScopeIdentitiesCache;
        static readonly ILogger Log = Logger.Factory.CreateLogger<IDeviceScopeIdentitiesCache>();

        enum EventIds
        {
            InitializingRefreshTask = IdStart,
            Created,
            ErrorInRefresh,
            StartingCycle,
            DoneCycle,
            ReceivedRequestToRefreshCache,
            RefreshSleepCompleted,
            RefreshSignalled,
            NotInScope,
            AddInScope,
            RefreshingServiceIdentity,
            GettingServiceIdentity,
            VerifyServiceIdentity
        }

        public static void Created() =>
            Log.LogInformation((int)EventIds.Created, "Created device scope identities cache");

        public static void ErrorInRefreshCycle(Exception exception)
        {
            Log.LogWarning((int)EventIds.ErrorInRefresh, "Encountered an error while refreshing the device scope identities cache. Will retry the operation in some time...");
            Log.LogDebug((int)EventIds.ErrorInRefresh, exception, "Error details while refreshing the device scope identities cache");
        }

        public static void StartingRefreshCycle() =>
            Log.LogInformation((int)EventIds.StartingCycle, "Starting refresh of device scope identities cache");

        public static void DoneRefreshCycle(TimeSpan refreshRate) =>
            Log.LogDebug((int)EventIds.DoneCycle, $"Done refreshing device scope identities cache. Waiting for {refreshRate.TotalMinutes} minutes.");

        public static void ErrorRefreshingCache(Exception exception, string deviceId)
        {
            Log.LogWarning((int)EventIds.ErrorInRefresh, exception, $"Error while refreshing the service identity for {deviceId}");
        }

        public static void ErrorProcessing(ServiceIdentity serviceIdentity, Exception exception)
        {
            string id = serviceIdentity?.Id ?? "unknown";
            Log.LogWarning((int)EventIds.ErrorInRefresh, exception, $"Error while processing the service identity for {id}");
        }

        public static void ReceivedRequestToRefreshCache() =>
            Log.LogDebug((int)EventIds.ReceivedRequestToRefreshCache, "Received request to refresh cache.");

        public static void RefreshSignalled() =>
            Log.LogDebug((int)EventIds.RefreshSignalled, "Device scope identities refresh is ready because a refresh was signalled.");

        public static void RefreshSleepCompleted() =>
            Log.LogDebug((int)EventIds.RefreshSleepCompleted, "Device scope identities refresh is ready because the wait period is over.");

        public static void NotInScope(string id) =>
            Log.LogDebug((int)EventIds.NotInScope, $"{id} is not in device scope, removing from cache.");

        public static void AddInScope(string id) =>
            Log.LogDebug((int)EventIds.AddInScope, $"{id} is in device scope, adding to cache.");

        public static void GettingServiceIdentity(string id) =>
            Log.LogDebug((int)EventIds.GettingServiceIdentity, $"Getting service identity for {id}");

        public static void RefreshingServiceIdentity(string id) =>
            Log.LogDebug((int)EventIds.RefreshingServiceIdentity, $"Refreshing service identity for {id}");

        internal static void InitializingRefreshTask(TimeSpan refreshRate) =>
            Log.LogDebug((int)EventIds.InitializingRefreshTask, $"Initializing device scope identities cache refresh task to run every {refreshRate.TotalMinutes} minutes.");

        internal static void VerifyServiceIdentityFailure(string id, string reason) =>
            Log.LogDebug((int)EventIds.VerifyServiceIdentity, $"Service identity {id} is not valid because: {reason}.");
    }
}
