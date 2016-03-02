using System;
using System.Collections.Generic;
using System.Text;
using EventStore.ClientAPI;
using Newtonsoft.Json;

namespace GesEventStore.GesIntegrations
{
    public static class JsonEventDeserializer
    {
        public static Event Deserialize(ResolvedEvent resolvedEvent)
        {
            var metadataJson = Encoding.UTF8.GetString(resolvedEvent.Event.Metadata);
            var metadata = JsonConvert.DeserializeObject<Dictionary<string, string>>(metadataJson);

            var messageTypeName = metadata["$type"];
            var messageType = Type.GetType(messageTypeName, false, false);
            var messageJson = Encoding.UTF8.GetString(resolvedEvent.Event.Data);
            var data = JsonConvert.DeserializeObject(messageJson, messageType);

            return new Event(metadata, data);
        }
    }
}
