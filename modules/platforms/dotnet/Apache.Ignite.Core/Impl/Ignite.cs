/*
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#pragma warning disable 618   // SpringConfigUrl
namespace Apache.Ignite.Core.Impl
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading.Tasks;
    using Apache.Ignite.Core.Binary;
    using Apache.Ignite.Core.Cache;
    using Apache.Ignite.Core.Cache.Configuration;
    using Apache.Ignite.Core.Cluster;
    using Apache.Ignite.Core.Compute;
    using Apache.Ignite.Core.Datastream;
    using Apache.Ignite.Core.DataStructures;
    using Apache.Ignite.Core.Events;
    using Apache.Ignite.Core.Impl.Binary;
    using Apache.Ignite.Core.Impl.Binary.Metadata;
    using Apache.Ignite.Core.Impl.Cache;
    using Apache.Ignite.Core.Impl.Cluster;
    using Apache.Ignite.Core.Impl.Common;
    using Apache.Ignite.Core.Impl.Datastream;
    using Apache.Ignite.Core.Impl.DataStructures;
    using Apache.Ignite.Core.Impl.Handle;
    using Apache.Ignite.Core.Impl.Transactions;
    using Apache.Ignite.Core.Impl.Unmanaged;
    using Apache.Ignite.Core.Lifecycle;
    using Apache.Ignite.Core.Messaging;
    using Apache.Ignite.Core.Services;
    using Apache.Ignite.Core.Transactions;
    using UU = Apache.Ignite.Core.Impl.Unmanaged.UnmanagedUtils;

    /// <summary>
    /// Native Ignite wrapper.
    /// </summary>
    internal class Ignite : IIgnite, IClusterGroupEx, ICluster
    {
        /** */
        private readonly IgniteConfiguration _cfg;

        /** Grid name. */
        private readonly string _name;

        /** Unmanaged node. */
        private readonly IUnmanagedTarget _proc;

        /** Marshaller. */
        private readonly Marshaller _marsh;

        /** Initial projection. */
        private readonly ClusterGroupImpl _prj;

        /** Binary. */
        private readonly Binary.Binary _binary;

        /** Cached proxy. */
        private readonly IgniteProxy _proxy;

        /** Lifecycle beans. */
        private readonly IList<LifecycleBeanHolder> _lifecycleBeans;

        /** Local node. */
        private IClusterNode _locNode;

        /** Transactions facade. */
        private readonly Lazy<TransactionsImpl> _transactions;

        /** Callbacks */
        private readonly UnmanagedCallbacks _cbs;

        /** Node info cache. */
        private readonly ConcurrentDictionary<Guid, ClusterNodeImpl> _nodes =
            new ConcurrentDictionary<Guid, ClusterNodeImpl>();

        /** Client reconnect task completion source. */
        private volatile TaskCompletionSource<bool> _clientReconnectTaskCompletionSource = 
            new TaskCompletionSource<bool>();

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="cfg">Configuration.</param>
        /// <param name="name">Grid name.</param>
        /// <param name="proc">Interop processor.</param>
        /// <param name="marsh">Marshaller.</param>
        /// <param name="lifecycleBeans">Lifecycle beans.</param>
        /// <param name="cbs">Callbacks.</param>
        public Ignite(IgniteConfiguration cfg, string name, IUnmanagedTarget proc, Marshaller marsh,
            IList<LifecycleBeanHolder> lifecycleBeans, UnmanagedCallbacks cbs)
        {
            Debug.Assert(cfg != null);
            Debug.Assert(proc != null);
            Debug.Assert(marsh != null);
            Debug.Assert(lifecycleBeans != null);
            Debug.Assert(cbs != null);

            _cfg = cfg;
            _name = name;
            _proc = proc;
            _marsh = marsh;
            _lifecycleBeans = lifecycleBeans;
            _cbs = cbs;

            marsh.Ignite = this;

            _prj = new ClusterGroupImpl(proc, UU.ProcessorProjection(proc), marsh, this, null);

            _binary = new Binary.Binary(marsh);

            _proxy = new IgniteProxy(this);

            cbs.Initialize(this);

            // Grid is not completely started here, can't initialize interop transactions right away.
            _transactions = new Lazy<TransactionsImpl>(
                    () => new TransactionsImpl(UU.ProcessorTransactions(proc), marsh, GetLocalNode().Id));

            // Set reconnected task to completed state for convenience.
            _clientReconnectTaskCompletionSource.SetResult(false);

            SetCompactFooter();
        }

        /// <summary>
        /// Sets the compact footer setting.
        /// </summary>
        private void SetCompactFooter()
        {
            if (!string.IsNullOrEmpty(_cfg.SpringConfigUrl))
            {
                // If there is a Spring config, use setting from Spring, 
                // since we ignore .NET config in legacy mode.
                var cfg0 = GetConfiguration().BinaryConfiguration;

                if (cfg0 != null)
                    _marsh.CompactFooter = cfg0.CompactFooter;
            }
        }

        /// <summary>
        /// On-start routine.
        /// </summary>
        internal void OnStart()
        {
            foreach (var lifecycleBean in _lifecycleBeans)
                lifecycleBean.OnStart(this);
        }

        /// <summary>
        /// Gets Ignite proxy.
        /// </summary>
        /// <returns>Proxy.</returns>
        public IgniteProxy Proxy
        {
            get { return _proxy; }
        }

        /** <inheritdoc /> */
        public string Name
        {
            get { return _name; }
        }

        /** <inheritdoc /> */

        public ICluster GetCluster()
        {
            return this;
        }

        /** <inheritdoc /> */
        IIgnite IClusterGroup.Ignite
        {
            get { return this; }
        }

        /** <inheritdoc /> */
        public IClusterGroup ForLocal()
        {
            return _prj.ForNodes(GetLocalNode());
        }

        /** <inheritdoc /> */
        public ICompute GetCompute()
        {
            return _prj.GetCompute();
        }

        /** <inheritdoc /> */
        public IClusterGroup ForNodes(IEnumerable<IClusterNode> nodes)
        {
            return ((IClusterGroup) _prj).ForNodes(nodes);
        }

        /** <inheritdoc /> */
        public IClusterGroup ForNodes(params IClusterNode[] nodes)
        {
            return _prj.ForNodes(nodes);
        }

        /** <inheritdoc /> */
        public IClusterGroup ForNodeIds(IEnumerable<Guid> ids)
        {
            return ((IClusterGroup) _prj).ForNodeIds(ids);
        }

        /** <inheritdoc /> */
        public IClusterGroup ForNodeIds(ICollection<Guid> ids)
        {
            return _prj.ForNodeIds(ids);
        }

        /** <inheritdoc /> */
        public IClusterGroup ForNodeIds(params Guid[] ids)
        {
            return _prj.ForNodeIds(ids);
        }

        /** <inheritdoc /> */
        public IClusterGroup ForPredicate(Func<IClusterNode, bool> p)
        {
            IgniteArgumentCheck.NotNull(p, "p");

            return _prj.ForPredicate(p);
        }

        /** <inheritdoc /> */
        public IClusterGroup ForAttribute(string name, string val)
        {
            return _prj.ForAttribute(name, val);
        }

        /** <inheritdoc /> */
        public IClusterGroup ForCacheNodes(string name)
        {
            return _prj.ForCacheNodes(name);
        }

        /** <inheritdoc /> */
        public IClusterGroup ForDataNodes(string name)
        {
            return _prj.ForDataNodes(name);
        }

        /** <inheritdoc /> */
        public IClusterGroup ForClientNodes(string name)
        {
            return _prj.ForClientNodes(name);
        }

        /** <inheritdoc /> */
        public IClusterGroup ForRemotes()
        {
            return _prj.ForRemotes();
        }

        /** <inheritdoc /> */
        public IClusterGroup ForHost(IClusterNode node)
        {
            IgniteArgumentCheck.NotNull(node, "node");

            return _prj.ForHost(node);
        }

        /** <inheritdoc /> */
        public IClusterGroup ForRandom()
        {
            return _prj.ForRandom();
        }

        /** <inheritdoc /> */
        public IClusterGroup ForOldest()
        {
            return _prj.ForOldest();
        }

        /** <inheritdoc /> */
        public IClusterGroup ForYoungest()
        {
            return _prj.ForYoungest();
        }

        /** <inheritdoc /> */
        public IClusterGroup ForDotNet()
        {
            return _prj.ForDotNet();
        }

        /** <inheritdoc /> */
        public ICollection<IClusterNode> GetNodes()
        {
            return _prj.GetNodes();
        }

        /** <inheritdoc /> */
        public IClusterNode GetNode(Guid id)
        {
            return _prj.GetNode(id);
        }

        /** <inheritdoc /> */
        public IClusterNode GetNode()
        {
            return _prj.GetNode();
        }

        /** <inheritdoc /> */
        public IClusterMetrics GetMetrics()
        {
            return _prj.GetMetrics();
        }

        /** <inheritdoc /> */
        [SuppressMessage("Microsoft.Usage", "CA1816:CallGCSuppressFinalizeCorrectly",
            Justification = "There is no finalizer.")]
        [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "_proxy",
            Justification = "Proxy does not need to be disposed.")]
        public void Dispose()
        {
            Ignition.Stop(Name, true);
        }

        /// <summary>
        /// Internal stop routine.
        /// </summary>
        /// <param name="cancel">Cancel flag.</param>
        internal unsafe void Stop(bool cancel)
        {
            UU.IgnitionStop(_proc.Context, Name, cancel);

            _cbs.Cleanup();

            foreach (var bean in _lifecycleBeans)
                bean.OnLifecycleEvent(LifecycleEventType.AfterNodeStop);
        }

        /** <inheritdoc /> */
        public ICache<TK, TV> GetCache<TK, TV>(string name)
        {
            return Cache<TK, TV>(UU.ProcessorCache(_proc, name));
        }

        /** <inheritdoc /> */
        public ICache<TK, TV> GetOrCreateCache<TK, TV>(string name)
        {
            return Cache<TK, TV>(UU.ProcessorGetOrCreateCache(_proc, name));
        }

        /** <inheritdoc /> */
        public ICache<TK, TV> GetOrCreateCache<TK, TV>(CacheConfiguration configuration)
        {
            IgniteArgumentCheck.NotNull(configuration, "configuration");

            using (var stream = IgniteManager.Memory.Allocate().GetStream())
            {
                var writer = Marshaller.StartMarshal(stream);

                configuration.Write(writer);

                stream.SynchronizeOutput();

                return Cache<TK, TV>(UU.ProcessorGetOrCreateCache(_proc, stream.MemoryPointer));
            }
        }

        /** <inheritdoc /> */
        public ICache<TK, TV> CreateCache<TK, TV>(string name)
        {
            return Cache<TK, TV>(UU.ProcessorCreateCache(_proc, name));
        }

        /** <inheritdoc /> */
        public ICache<TK, TV> CreateCache<TK, TV>(CacheConfiguration configuration)
        {
            IgniteArgumentCheck.NotNull(configuration, "configuration");

            using (var stream = IgniteManager.Memory.Allocate().GetStream())
            {
                var writer = Marshaller.StartMarshal(stream);

                configuration.Write(writer);

                stream.SynchronizeOutput();

                return Cache<TK, TV>(UU.ProcessorCreateCache(_proc, stream.MemoryPointer));
            }
        }

        /** <inheritdoc /> */
        public void DestroyCache(string name)
        {
            UU.ProcessorDestroyCache(_proc, name);
        }

        /// <summary>
        /// Gets cache from specified native cache object.
        /// </summary>
        /// <param name="nativeCache">Native cache.</param>
        /// <param name="keepBinary">Keep binary flag.</param>
        /// <returns>
        /// New instance of cache wrapping specified native cache.
        /// </returns>
        public ICache<TK, TV> Cache<TK, TV>(IUnmanagedTarget nativeCache, bool keepBinary = false)
        {
            return new CacheImpl<TK, TV>(this, nativeCache, _marsh, false, keepBinary, false, false);
        }

        /** <inheritdoc /> */
        public IClusterNode GetLocalNode()
        {
            return _locNode ?? (_locNode = GetNodes().FirstOrDefault(x => x.IsLocal));
        }

        /** <inheritdoc /> */
        public bool PingNode(Guid nodeId)
        {
            return _prj.PingNode(nodeId);
        }

        /** <inheritdoc /> */
        public long TopologyVersion
        {
            get { return _prj.TopologyVersion; }
        }

        /** <inheritdoc /> */
        public ICollection<IClusterNode> GetTopology(long ver)
        {
            return _prj.Topology(ver);
        }

        /** <inheritdoc /> */
        public void ResetMetrics()
        {
            UU.ProjectionResetMetrics(_prj.Target);
        }

        /** <inheritdoc /> */
        public Task<bool> ClientReconnectTask
        {
            get { return _clientReconnectTaskCompletionSource.Task; }
        }

        /** <inheritdoc /> */
        public IDataStreamer<TK, TV> GetDataStreamer<TK, TV>(string cacheName)
        {
            return new DataStreamerImpl<TK, TV>(UU.ProcessorDataStreamer(_proc, cacheName, false),
                _marsh, cacheName, false);
        }

        /** <inheritdoc /> */
        public IBinary GetBinary()
        {
            return _binary;
        }

        /** <inheritdoc /> */
        public ICacheAffinity GetAffinity(string cacheName)
        {
            return new CacheAffinityImpl(UU.ProcessorAffinity(_proc, cacheName), _marsh, false, this);
        }

        /** <inheritdoc /> */

        public ITransactions GetTransactions()
        {
            return _transactions.Value;
        }

        /** <inheritdoc /> */
        public IMessaging GetMessaging()
        {
            return _prj.GetMessaging();
        }

        /** <inheritdoc /> */
        public IEvents GetEvents()
        {
            return _prj.GetEvents();
        }

        /** <inheritdoc /> */
        public IServices GetServices()
        {
            return _prj.GetServices();
        }

        /** <inheritdoc /> */
        public IAtomicLong GetAtomicLong(string name, long initialValue, bool create)
        {
            IgniteArgumentCheck.NotNullOrEmpty(name, "name");

            var nativeLong = UU.ProcessorAtomicLong(_proc, name, initialValue, create);

            if (nativeLong == null)
                return null;

            return new AtomicLong(nativeLong, Marshaller, name);
        }

        /** <inheritdoc /> */
        public IAtomicSequence GetAtomicSequence(string name, long initialValue, bool create)
        {
            IgniteArgumentCheck.NotNullOrEmpty(name, "name");

            var nativeSeq = UU.ProcessorAtomicSequence(_proc, name, initialValue, create);

            if (nativeSeq == null)
                return null;

            return new AtomicSequence(nativeSeq, Marshaller, name);
        }

        /** <inheritdoc /> */
        public IAtomicReference<T> GetAtomicReference<T>(string name, T initialValue, bool create)
        {
            IgniteArgumentCheck.NotNullOrEmpty(name, "name");

            var refTarget = GetAtomicReferenceUnmanaged(name, initialValue, create);

            return refTarget == null ? null : new AtomicReference<T>(refTarget, Marshaller, name);
        }

        /// <summary>
        /// Gets the unmanaged atomic reference.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="initialValue">The initial value.</param>
        /// <param name="create">Create flag.</param>
        /// <returns>Unmanaged atomic reference, or null.</returns>
        private IUnmanagedTarget GetAtomicReferenceUnmanaged<T>(string name, T initialValue, bool create)
        {
            IgniteArgumentCheck.NotNullOrEmpty(name, "name");

            // Do not allocate memory when default is not used.
            if (!create)
                return UU.ProcessorAtomicReference(_proc, name, 0, false);
            
            using (var stream = IgniteManager.Memory.Allocate().GetStream())
            {
                var writer = Marshaller.StartMarshal(stream);

                writer.Write(initialValue);

                var memPtr = stream.SynchronizeOutput();

                return UU.ProcessorAtomicReference(_proc, name, memPtr, true);
            }
        }

        /** <inheritdoc /> */
        public IgniteConfiguration GetConfiguration()
        {
            using (var stream = IgniteManager.Memory.Allocate(1024).GetStream())
            {
                UU.ProcessorGetIgniteConfiguration(_proc, stream.MemoryPointer);

                stream.SynchronizeInput();

                return new IgniteConfiguration(_marsh.StartUnmarshal(stream));
            }
        }

        /// <summary>
        /// Gets internal projection.
        /// </summary>
        /// <returns>Projection.</returns>
        internal ClusterGroupImpl ClusterGroup
        {
            get { return _prj; }
        }

        /// <summary>
        /// Marshaller.
        /// </summary>
        internal Marshaller Marshaller
        {
            get { return _marsh; }
        }

        /// <summary>
        /// Configuration.
        /// </summary>
        internal IgniteConfiguration Configuration
        {
            get { return _cfg; }
        }

        /// <summary>
        /// Put metadata to Grid.
        /// </summary>
        /// <param name="metas">Metadata.</param>
        internal void PutBinaryTypes(ICollection<BinaryType> metas)
        {
            _prj.PutBinaryTypes(metas);
        }

        /** <inheritDoc /> */
        public IBinaryType GetBinaryType(int typeId)
        {
            return _prj.GetBinaryType(typeId);
        }

        /// <summary>
        /// Handle registry.
        /// </summary>
        public HandleRegistry HandleRegistry
        {
            get { return _cbs.HandleRegistry; }
        }

        /// <summary>
        /// Updates the node information from stream.
        /// </summary>
        /// <param name="memPtr">Stream ptr.</param>
        public void UpdateNodeInfo(long memPtr)
        {
            var stream = IgniteManager.Memory.Get(memPtr).GetStream();

            IBinaryRawReader reader = Marshaller.StartUnmarshal(stream, false);

            var node = new ClusterNodeImpl(reader);

            node.Init(this);

            _nodes[node.Id] = node;
        }

        /// <summary>
        /// Gets the node from cache.
        /// </summary>
        /// <param name="id">Node id.</param>
        /// <returns>Cached node.</returns>
        public ClusterNodeImpl GetNode(Guid? id)
        {
            return id == null ? null : _nodes[id.Value];
        }

        /// <summary>
        /// Gets the interop processor.
        /// </summary>
        internal IUnmanagedTarget InteropProcessor
        {
            get { return _proc; }
        }

        /// <summary>
        /// Called when local client node has been disconnected from the cluster.
        /// </summary>
        public void OnClientDisconnected()
        {
            _clientReconnectTaskCompletionSource = new TaskCompletionSource<bool>();
        }

        /// <summary>
        /// Called when local client node has been reconnected to the cluster.
        /// </summary>
        /// <param name="clusterRestarted">Cluster restarted flag.</param>
        public void OnClientReconnected(bool clusterRestarted)
        {
            _clientReconnectTaskCompletionSource.TrySetResult(clusterRestarted);
        }
    }
}
