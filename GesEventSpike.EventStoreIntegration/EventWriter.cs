using System.Linq;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using GesEventSpike.Core;
using Newtonsoft.Json;

namespace GesEventSpike.EventStoreIntegration
{
    public class EventWriter
    {
        public static async Task<WriteResult> Write(Envelope<MessageContext, WriteToStream> envelope, IEventStoreConnection connection, JsonSerializerSettings settings)
        {
            var eventData = EventSerializer.Serialize(envelope.Body.Id, envelope.Body.Data, envelope.Body.Metadata, settings);
            envelope.Body.Metadata["$correlationId"] = envelope.Header.MetadataLookup["$correlationId"].FirstOrDefault();
            envelope.Body.Metadata["$causationId"] = envelope.Header.EventId;
            envelope.Body.Metadata["tenantId"] = envelope.Header.MetadataLookup["tenantId"].FirstOrDefault();

            return await connection.AppendToStreamAsync(envelope.Body.StreamId, ExpectedVersion.Any, eventData);
        }
    }
}