using System.Collections.Generic;

namespace GesEventStore.GesIntegrations
{
    public class Event
    {
        public readonly IDictionary<string, string> Metadata;
        public readonly object Data;

        public Event(IDictionary<string, string> metadata, object data)
        {
            Metadata = metadata;
            Data = data;
        }
    }
}