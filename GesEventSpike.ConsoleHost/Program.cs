using System;
using System.Collections.Generic;
using System.Net;
using EventStore.ClientAPI;
using GesEventSpike.EventStoreIntegration;

namespace GesEventSpike.ConsoleHost
{
    internal class Program
    {
        private static readonly EventData[] EventsBuffer = new EventData[1];

        private static void Main(string[] args)
        {
            var runtime = Runtime.StartNewAsync().Result;

            var connection = EventStoreConnection
                .Create(ConnectionSettings.Create().Build(), new IPEndPoint(IPAddress.Loopback, 1113));

            connection.ConnectAsync().Wait();

            Console.WriteLine(@"Type ""q"" to quit");
            Console.WriteLine(@"Type ""a"" to append an event");

            string input;
            do
            {
                input = Console.ReadLine();

                if (input != "a") continue;

                var @event = new ItemPurchased(GuidEncoder.Encode(Guid.NewGuid()));
                var metadata = new Dictionary<string, string>
                {
                    {"tenantId", "Warehouse A"},
                    {"$correlationId", GuidEncoder.Encode(Guid.NewGuid())}
                };
                var eventData = EventSerializer.Serialize(Guid.NewGuid(), @event, metadata);

                EventsBuffer[0] = eventData;
                connection.AppendToStreamAsync("ingress", ExpectedVersion.Any, EventsBuffer).Wait();

                Console.WriteLine("Appended event");
            } while (!string.Equals(input, "q"));

            runtime.Stop();
            Console.WriteLine("Stopping...");
        }
    }
}
