using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using GesEventSpike.Core;
using Newtonsoft.Json;

namespace GesEventSpike.EventStoreIntegration
{
    public class EventStoreHandlers
    {
        public static IEnumerable<Envelope<MessageContext, object>> Deserialize(RecordedEvent recordedEvent, ILookup<string, Type> typeLookup, JsonSerializerSettings settings = null)
        {
            var additionalMetadata = EventSerializer.DeserializeMetadata(recordedEvent, settings);
            var message = EventSerializer.Deserialize(recordedEvent, typeLookup, settings);

            var streamContext = new StreamContext(recordedEvent.EventNumber, recordedEvent.EventStreamId);
            var messageContext = new MessageContext(recordedEvent.EventId, streamContext, additionalMetadata);

            yield return Envelope.Create(messageContext, message);
        }

        public static IEnumerable<Task<WriteResult>> WriteAsync(Envelope<MessageContext, WriteToStream> envelope, IEventStoreConnection connection, JsonSerializerSettings settings = null)
        {
            yield return EventWriter.Write(envelope, connection, settings);
        }
    }
}