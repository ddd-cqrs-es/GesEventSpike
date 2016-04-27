using System;
using System.Linq;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using GesEventSpike.Core;
using Newtonsoft.Json;

namespace GesEventSpike.EventStoreIntegration
{
    public class EventStoreHandlers
    {
        public static Tuple<MessageContext, object> Deserialize(RecordedEvent recordedEvent, ILookup<string, Type> typeLookup, JsonSerializerSettings settings = null)
        {
            var additionalMetadata = EventSerializer.DeserializeMetadata(recordedEvent, settings);
            var message = EventSerializer.Deserialize(recordedEvent, typeLookup, settings);

            var streamContext = new StreamContext(recordedEvent.EventNumber, recordedEvent.EventStreamId);
            var messageContext = new MessageContext(recordedEvent.EventId, streamContext, additionalMetadata);

            return Tuple.Create(messageContext, message);
        }

        public static async Task<WriteResult> WriteAsync(MessageContext messageContext, WriteToStream streamWrite, IEventStoreConnection connection, JsonSerializerSettings settings = null)
        {
            streamWrite.Metadata["$causationId"] = messageContext.EventId;
            streamWrite.Metadata["$correlationId"] = messageContext.MetadataLookup["$correlationId"].FirstOrDefault();
            streamWrite.Metadata["tenantId"] = messageContext.MetadataLookup["tenantId"].FirstOrDefault();

            var eventData = EventSerializer.Serialize(streamWrite.Id, streamWrite.Data, streamWrite.Metadata, settings);

            return await connection.AppendToStreamAsync(streamWrite.StreamId, ExpectedVersion.Any, eventData);
        }
    }
}