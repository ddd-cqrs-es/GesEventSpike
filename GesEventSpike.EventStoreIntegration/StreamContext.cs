namespace GesEventSpike.EventStoreIntegration
{
    public class StreamContext
    {
        public readonly int EventNumber;
        public readonly string StreamId;

        public static readonly StreamContext None = new StreamContext();

        private StreamContext()
        {
        }

        public StreamContext(int eventNumber, string streamId)
        {
            EventNumber = eventNumber;
            StreamId = streamId;
        }
    }
}