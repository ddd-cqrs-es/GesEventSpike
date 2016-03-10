using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using GesEventSpike.Core;
using GesEventSpike.EventStoreIntegration;
using GesEventSpike.Matching;
using StackExchange.Redis;

namespace GesEventSpike.MatchingHost
{
    public class Runtime
    {
        private static readonly DeterministicGuid
            EventId = new DeterministicGuid(new Guid("DD06AB31-815E-4038-AE86-5C5FF6CC4FCB"));

        private static readonly ILookup<string, Type> MessageTypeLookup = new[]
        {
            typeof (int)
        }.ToLookup(keyIs => keyIs.Name, valueIs => valueIs, StringComparer.OrdinalIgnoreCase);

        private readonly IEventStoreConnection _eventStoreConnection;
        private readonly ConnectionMultiplexer _redisConnection;
        private readonly Dispatcher _mainDispatcher;

        public static async Task<Runtime> StartNewAsync()
        {
            var connectionSettings = ConnectionSettings.Create()
                .KeepReconnecting()
                .KeepRetrying()
                .Build();

            var connection = EventStoreConnection
                .Create(connectionSettings, new IPEndPoint(IPAddress.Loopback, 1113));

            await connection.ConnectAsync();

            var redisConnection = await ConnectionMultiplexer.ConnectAsync(new ConfigurationOptions
            {
                EndPoints = {{IPAddress.Loopback, 6379}},
                AllowAdmin = true
            });

            var database = redisConnection.GetDatabase();

            var streamCheckpoint = new[] {database.StringGet("matching:checkpoint")}
                .Where(result => result.HasValue && result.IsInteger)
                .Cast<int?>()
                .DefaultIfEmpty(StreamCheckpoint.StreamStart)
                .First();

            return new Runtime(connection, redisConnection, streamCheckpoint);
        }

        private Runtime(IEventStoreConnection eventStoreConnection, ConnectionMultiplexer redisConnection, int? streamCheckpoint)
        {
            _eventStoreConnection = eventStoreConnection;
            _redisConnection = redisConnection;

            _mainDispatcher = new Dispatcher();

            _mainDispatcher.Register<ResolvedEvent>(resolvedEvent => EventStoreHandlers
                .Deserialize(resolvedEvent.Event, MessageTypeLookup));
            
            _mainDispatcher.Register<Envelope<MessageContext, DemographicsDocument>>(envelope =>
            {
                return IndexAndQueryBestMatch(envelope).Result;
            });

            _eventStoreConnection.SubscribeToStreamFrom("matching", streamCheckpoint, false, (subscription, resolvedEvent) =>
            {
                _mainDispatcher.DispatchExhaustive(resolvedEvent);
            });
        }
        
        private async Task<IEnumerable<object>> IndexAndQueryBestMatch(Envelope<MessageContext, DemographicsDocument> envelope)
        {
            var database = _redisConnection.GetDatabase();
            var transaction = database.CreateTransaction();

            var document = envelope.Body;

            var essenceId = await MatchingHandlers.GetOrCreateEssence(document, transaction);

            var matchedEvent = new DemographicsMatched(document.DocumentId, essenceId);
            var eventId = EventId.Create(envelope.Header.EventId);
            var writeEvent = new WriteToStream(eventId, "matched", matchedEvent);

            return new[] {writeEvent};
        }
    }

    public class DemographicsMatched
    {
        public readonly string DocumentId;
        public readonly string EssenceId;

        public DemographicsMatched(string documentId, string essenceId)
        {
            DocumentId = documentId;
            EssenceId = essenceId;
        }
    }
}