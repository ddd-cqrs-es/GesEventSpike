namespace GesEventSpike.ConsoleHost
{
    public class CheckpointEvent
    {
        public readonly int Position;

        public CheckpointEvent(int position)
        {
            Position = position;
        }
    }
}