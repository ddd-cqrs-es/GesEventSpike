using System;
using System.Collections.Generic;

namespace GesEventSpike.EventStoreIntegration
{
    public class WriteToStream
    {
        public readonly Guid Id;
        public readonly string StreamId;
        public readonly object Data;
        public readonly IDictionary<string, object> Metadata;

        public WriteToStream(Guid id, string streamId, object data, IDictionary<string, object> metadata = null)
        {
            Id = id;
            StreamId = streamId;
            Data = data;
            Metadata = metadata ?? new Dictionary<string, object>();
        }
    }
}