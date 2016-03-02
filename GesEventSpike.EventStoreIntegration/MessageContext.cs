using System;
using System.Collections.Generic;
using System.Linq;

namespace GesEventSpike.EventStoreIntegration
{
  public class MessageContext
  {
    public readonly Guid EventId;
    public readonly StreamContext StreamContext;
    public readonly ILookup<string, object> MetadataLookup;

    public MessageContext(Guid eventId, StreamContext streamContext, IDictionary<string, object> metadata)
    {
      EventId = eventId;
      StreamContext = streamContext;
      MetadataLookup = metadata
          .ToLookup(keyIs => keyIs.Key, valueIs => valueIs.Value);
    }
  }
}