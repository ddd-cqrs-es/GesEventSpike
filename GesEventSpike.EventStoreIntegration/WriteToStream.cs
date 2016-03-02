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

        public WriteToStream(Guid id, string streamId, object data, IDictionary<string, object> metadata)
        {
            Id = id;
            StreamId = streamId;
            Data = data;
            Metadata = metadata;
        }
    }
}