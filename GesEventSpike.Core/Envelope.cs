using System;

namespace GesEventSpike.Core
{
    public class Envelope
    {
        private static readonly Type EnvelopeType = typeof (Envelope<,>);

        public static object CreateGeneric<THeader>(THeader header, object body)
        {
            var genericType = EnvelopeType.MakeGenericType(typeof (THeader), body.GetType());
            return Activator.CreateInstance(genericType, header, body);
        }

        public static Envelope<THeader, TBody> Create<THeader, TBody>(THeader header, TBody body)
        {
            return new Envelope<THeader, TBody>(header, body);
        }
    }

    public class Envelope<THeader, TBody>
    {
        public readonly THeader Header;
        public readonly TBody Body;

        public Envelope(THeader header, TBody body)
        {
            Header = header;
            Body = body;
        }
    }
}