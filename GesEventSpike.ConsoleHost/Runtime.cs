using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using EventStore.ClientAPI;
using GesEventSpike.Core;
using GesEventSpike.EventStoreIntegration;

namespace GesEventSpike.ConsoleHost
{
    internal class Runtime
    {
        private static readonly ILookup<string, Type> MessageTypeLookup = new[]
        {
            typeof (ItemPurchased),
            typeof (CheckpointEvent)
        }.ToLookup(keyIs => keyIs.Name, valueIs => valueIs, StringComparer.OrdinalIgnoreCase);

        private static readonly DeterministicGuid EventId = new DeterministicGuid(new Guid("B7441838-37F6-47D6-B74B-A268690BA312"));

        private readonly TaskCompletionSource<Nothing> _initializedCompletionSource = new TaskCompletionSource<Nothing>(); 
        private readonly IEventStoreConnection _eventStoreConnection;
        private readonly ConcurrentDictionary<string, Nothing> _hasInitialized = new ConcurrentDictionary<string, Nothing>();
        private readonly ITargetBlock<ResolvedEvent> _inputBlock;

        private EventStoreStreamCatchUpSubscription _ingressSubscription;

        public static async Task<Runtime> StartNewAsync()
        {
            var connectionSettings = ConnectionSettings.Create()
                .KeepReconnecting()
                .KeepRetrying()
                .Build();

            var connection = EventStoreConnection
                .Create(connectionSettings, new IPEndPoint(IPAddress.Loopback, 1113));

            var instance = new Runtime(connection);

            connection.Connected += instance.OnConnected;
            
            await connection.ConnectAsync();
            await instance._initializedCompletionSource.Task;

            return instance;
        }

        private async void OnConnected(object sender, ClientConnectionEventArgs connectedEvent)
        {
            var readResult = await _eventStoreConnection.ReadEventAsync("egress", StreamPosition.End, false);

            var streamCheckpoint = new[] { readResult.Event }
                .Where(maybeEvent => maybeEvent.HasValue)
                .Select(resolvedEvent => EventSerializer.Deserialize(resolvedEvent.Value.Event, MessageTypeLookup))
                .Cast<CheckpointEvent>()
                .Select(checkpointEvent => checkpointEvent.Position as int?)
                .DefaultIfEmpty(StreamCheckpoint.StreamStart)
                .First();

            _ingressSubscription = _eventStoreConnection.SubscribeToStreamFrom("ingress", streamCheckpoint, false, OnEvent);

            _initializedCompletionSource.TrySetResult(Nothing.Value);
        }

        private void OnEvent(EventStoreCatchUpSubscription subscription, ResolvedEvent resolvedEvent)
        {
            _inputBlock.SendAsync(resolvedEvent).Wait();
        }

        private Runtime(IEventStoreConnection eventStoreConnection)
        {
            _eventStoreConnection = eventStoreConnection;

            var deserializerBlock = new TransformBlock<ResolvedEvent, Tuple<MessageContext, object>>(resolvedEvent => EventStoreHandlers
                .Deserialize(resolvedEvent.Event, MessageTypeLookup));
            
            var batchBlock = new BatchBlock<Tuple<MessageContext, object>>(512);

            deserializerBlock.LinkTo(batchBlock);

            var tenantSplitBlock = new TransformManyBlock<Tuple<MessageContext, object>[], Tuple<string, Tuple<MessageContext, object>[]>>(batchedEnvelopes =>
            {
                return batchedEnvelopes
                    .GroupBy(envelope => envelope.Item1.MetadataLookup["tenantId"].OfType<string>().FirstOrDefault())
                    .Select(group => Tuple.Create(group.Key, group.ToArray()));
            });

            batchBlock.LinkTo(tenantSplitBlock);

            var handlerBlock = new ActionBlock<Tuple<string, Tuple<MessageContext, object>[]>>(envelope =>
            {
                var tenantId = envelope.Item1;
            });

            tenantSplitBlock.LinkTo(handlerBlock);

            var eventWriterBlock = new ActionBlock<object>(async envelope =>
            {
                await EventStoreHandlers.WriteAsync((Envelope<MessageContext, WriteToStream>)envelope, _eventStoreConnection);
            });

            //handlerBlock.LinkTo(eventWriterBlock, message => message is Envelope<MessageContext, WriteToStream>);

            //handlerBlock.LinkTo(projectorBatchBlock, message => message is SqlNonQueryCommand);

            //var projectorBlock = new ActionBlock<SqlNonQueryCommand>(envelope =>
            //{
                
            //});



            //var connectionSettingsFactory = new MultitenantSqlConnectionSettingsFactory(tenantId);
            //var connectionStringSettings = connectionSettingsFactory.GetSettings("Projections");
            //var connectionString = connectionStringSettings.ConnectionString;

            //_hasInitialized.GetOrAdd(tenantId, _ =>
            //{
            //    var executor = new SqlCommandExecutor(connectionStringSettings);
            //    executor.ExecuteNonQuery(InventoryProjectionHandlers.CreateSchema());
            //    return Nothing.Value;
            //});

            //var sqlConnection = new SqlConnection(connectionString);
            //sqlConnection.Open();

            //using (sqlConnection)
            //using (var transaction = sqlConnection.BeginTransaction())
            //{
            //    var sqlExecutor = new ConnectedTransactionalSqlCommandExecutor(transaction);

            //    dispatcher.Register<SqlNonQueryCommand>(command =>
            //    {
            //        sqlExecutor.ExecuteNonQuery(command);
            //        return Enumerable.Empty<object>();
            //    });

            //    var outbox = await dispatcher.DispatchExhaustiveAsync(envelope.Body);

            //    transaction.Commit();

            //    return outbox;
            //}
            
            _inputBlock = deserializerBlock;
        }

        private async Task<IEnumerable<object>> ScopedHandle(Envelope<MessageContext, object> envelope)
        {
            var tenantId = envelope.Header.MetadataLookup["tenantId"].First() as string;
            if (tenantId == null) return Enumerable.Empty<object>();

            var dispatcher = new Dispatcher();

            var eventId = EventId.Create(envelope.Header.EventId);

            dispatcher.Register<ItemPurchased>(itemPurchased => InventoryProjectionHandlers
                .Project(itemPurchased, eventId, envelope.Header.StreamContext.EventNumber));
        }

        public void Stop()
        {
            _ingressSubscription.Stop();
        }
    }
}