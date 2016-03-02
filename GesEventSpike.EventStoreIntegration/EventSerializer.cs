using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EventStore.ClientAPI;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace GesEventSpike.EventStoreIntegration
{
    public static class EventSerializer
    {
        public static readonly JsonSerializerSettings DefaultSerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
        };
        
        public static EventData Serialize(Guid eventId, object data, object metadata, JsonSerializerSettings settings = null)
        {
            settings = settings ?? DefaultSerializerSettings;

            var dataJson = JsonConvert.SerializeObject(data, settings);
            var dataBytes = Encoding.UTF8.GetBytes(dataJson);

            var metadataJson = JsonConvert.SerializeObject(metadata, settings);
            var metadataBytes = Encoding.UTF8.GetBytes(metadataJson);

            return new EventData(eventId, data.GetType().Name, true, dataBytes, metadataBytes);
        }

        public static object Deserialize(RecordedEvent recordedEvent, ILookup<string, Type> typeLookup, JsonSerializerSettings settings = null)
        {
            settings = settings ?? DefaultSerializerSettings;

            var messageTypeName = recordedEvent.EventType;

            var messageType = typeLookup[messageTypeName].First();
            var messageJson = Encoding.UTF8.GetString(recordedEvent.Data);

            return JsonConvert.DeserializeObject(messageJson, messageType, settings);
        }

        public static IDictionary<string, object> DeserializeMetadata(RecordedEvent recordedEvent, JsonSerializerSettings settings = null)
        {
            settings = settings ?? DefaultSerializerSettings;

            var metadataJson = Encoding.UTF8.GetString(recordedEvent.Metadata);
            return JsonConvert.DeserializeObject<Dictionary<string, object>>(metadataJson, settings);
        }
    }
}
