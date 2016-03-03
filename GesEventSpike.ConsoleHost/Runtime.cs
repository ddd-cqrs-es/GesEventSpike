using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using GesEventSpike.Core;
using GesEventSpike.EventStoreIntegration;
using Paramol;
using Paramol.Executors;

namespace GesEventSpike.ConsoleHost
{
    internal class Runtime
    {
        private static readonly ILookup<string, Type> MessageTypeLookup = new[]
        {
            typeof (ItemPurchased),
            typeof (CheckpointEvent)
        }.ToLookup(keyIs => keyIs.Name, valueIs => valueIs, StringComparer.OrdinalIgnoreCase);

        private static readonly DeterministicGuid DeterministicGuid = new DeterministicGuid(new Guid("B7441838-37F6-47D6-B74B-A268690BA312"));

        private readonly EventStoreStreamCatchUpSubscription _ingressSubscription;
        private readonly IEventStoreConnection _eventStoreLiveConnection;
        private readonly Dispatcher _mainDispatcher;
        private readonly ConcurrentDictionary<string, Nothing> _hasInitialized = new ConcurrentDictionary<string, Nothing>(); 

        public static async Task<Runtime> StartNewAsync()
        {
            var connectionSettings = ConnectionSettings.Create()
                .KeepReconnecting()
                .KeepRetrying()
                .Build();

            var connection = EventStoreConnection
                .Create(connectionSettings, new IPEndPoint(IPAddress.Loopback, 1113));

            await connection.ConnectAsync();

            var readResult = await connection.ReadEventAsync("egress", StreamPosition.End, false);

            var streamCheckpoint = new[] {readResult.Event}
                .Where(maybeEvent => maybeEvent.HasValue)
                .Select(resolvedEvent => EventSerializer.Deserialize(resolvedEvent.Value.Event, MessageTypeLookup))
                .Cast<CheckpointEvent>()
                .Select(checkpointEvent => checkpointEvent.Position as int?)
                .DefaultIfEmpty(StreamCheckpoint.StreamStart)
                .First();
            
            return new Runtime(connection, streamCheckpoint);
        }

        private Runtime(IEventStoreConnection liveConnection, int? streamCheckpoint)
        {
            _eventStoreLiveConnection = liveConnection;

            _mainDispatcher = new Dispatcher();

            _mainDispatcher.Register<ResolvedEvent>(resolvedEvent => EventStoreHandlers
                .Deserialize(resolvedEvent.Event, MessageTypeLookup));

            _mainDispatcher.Register<Envelope<MessageContext, object>>(ScopedHandle);

            _ingressSubscription = _eventStoreLiveConnection.SubscribeToStreamFrom("ingress", streamCheckpoint, false, (subscription, resolvedEvent) =>
            {
                _mainDispatcher.DispatchExhaustive(resolvedEvent);
            });
        }

        private IEnumerable<object> ScopedHandle(Envelope<MessageContext, object> mainEnvelope)
        {
            var tenantId = mainEnvelope.Header.MetadataLookup["tenantId"].First() as string;
            if (tenantId == null) return new[] {new NotHandled(mainEnvelope)};

            var scopedDispatcher = new Dispatcher();

            scopedDispatcher.Register<WriteToStream>(writeToStream =>
            {
                var envelope = Envelope.Create(mainEnvelope.Header, writeToStream);
                Task.WhenAll(EventStoreHandlers.WriteAsync(envelope, _eventStoreLiveConnection)).Wait();
                return Enumerable.Empty<object>();
            });

            scopedDispatcher.Register<Envelope<MessageContext, ItemPurchased>>(envelope =>
            {
                var eventId = DeterministicGuid.Create(mainEnvelope.Header.EventId);
                return InventoryProjectionHandlers.Project(envelope.Body, eventId, envelope.Header.StreamContext.EventNumber);
            });

            var connectionSettingsFactory = new MultitenantSqlConnectionSettingsFactory(tenantId);
            var connectionStringSettings = connectionSettingsFactory.GetSettings("Projections");
            var connectionString = connectionStringSettings.ConnectionString;

            _hasInitialized.GetOrAdd(tenantId, _ =>
            {
                var executor = new SqlCommandExecutor(connectionStringSettings);
                executor.ExecuteNonQuery(InventoryProjectionHandlers.CreateSchema());
                return Nothing.Value;
            });

            var sqlConnection = new SqlConnection(connectionString);
            sqlConnection.Open();

            using (sqlConnection)
            using (var transaction = sqlConnection.BeginTransaction())
            {
                var sqlExecutor = new ConnectedTransactionalSqlCommandExecutor(transaction);

                scopedDispatcher.Register<SqlNonQueryCommand>(command =>
                {
                    sqlExecutor.ExecuteNonQuery(command);
                    return Enumerable.Empty<object>();
                });

                var typedMainEnvelope = Envelope.CreateGeneric(mainEnvelope.Header, mainEnvelope.Body);
                var unhandled = scopedDispatcher.DispatchExhaustive(typedMainEnvelope);

                transaction.Commit();

                return unhandled;
            }
        }

        public void Stop()
        {
            _ingressSubscription.Stop();
        }
    }
}