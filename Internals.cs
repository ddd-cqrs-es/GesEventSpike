using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Security.Cryptography;
using System.Text;

namespace GesEventSpike
{
    internal static class ExtensionsForString
    {
        public static string StringJoin(this IEnumerable<string> source, string seperator)
        {
            return string.Join(seperator, source);
        }
    }

    internal static class ResourceCache
    {
        private static readonly Assembly SelfAssembly = typeof(ResourceCache).Assembly;
        private static readonly ConcurrentDictionary<string, string> Cache = new ConcurrentDictionary<string, string>();

        public static string GetString(string @namespace, params string[] nameParts)
        {
            var resourceName = string.Concat(new[] {@namespace, "."}.Concat(nameParts));
            return Cache.GetOrAdd(resourceName, _ =>
            {
                using (var stream = SelfAssembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) throw new MissingManifestResourceException(resourceName);
                    using (var reader = new StreamReader(stream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            });
        }
    }

    internal class Nothing
    {
        private Nothing() { }
        public static readonly Nothing Value = new Nothing();
    }

    internal class NotHandled
    {
        public readonly object Message;

        public NotHandled(object message)
        {
            Message = message;
        }
    }

    internal class Dispatcher
    {
        private readonly Dictionary<Type, Func<object, IEnumerable<object>>> _lookup = new Dictionary<Type, Func<object, IEnumerable<object>>>();

        public void Register<T>(Func<T, IEnumerable<object>> handler)
        {
            _lookup.Add(typeof (T), message => handler((T) message));
        }
        
        public IEnumerable<object> Dispatch<TMessage>(TMessage message)
        {
            Func<object, IEnumerable<object>> handler;
            return _lookup.TryGetValue(message.GetType(), out handler)
                ? handler(message)
                : new[] {new NotHandled(message)};
        }
        
        public IEnumerable<object> DispatchExhaustive(object initialMessage)
        {
            var unhandledOutbox = new List<object>();

            var outbox = new[] { initialMessage };
            do
            {
                var results = outbox.Select(Dispatch)
                    .Select(_ => _.ToArray())
                    .SelectMany(_ => _)
                    .ToArray()
                    .ToLookup(message => message is NotHandled);

                outbox = results[false].ToArray();

                unhandledOutbox.AddRange(results[true]
                    .OfType<NotHandled>()
                    .Select(notHandled => notHandled.Message));
            } while (outbox.Any());

            return unhandledOutbox;
        }
    }

    internal class CompositeDisposable : IDisposable
    {
        private readonly IEnumerable<IDisposable> _instances;

        public CompositeDisposable(params IDisposable[] instances)
        {
            _instances = instances;
        }

        public void Dispose()
        {
            foreach (var instance in _instances)
            {
                instance.Dispose();
            }
        }
    }

    internal class DisposeCallback : IDisposable
    {
        readonly Action _disposeCallback;

        public DisposeCallback(Action disposeCallback)
        {
            if (disposeCallback == null) throw new ArgumentNullException("disposeCallback");

            _disposeCallback = disposeCallback;
        }

        public void Dispose()
        {
            _disposeCallback();
        }
    }

    // http://madskristensen.net/post/a-shorter-and-url-friendly-guid
    internal static class GuidEncoder
    {
        public static string Encode(string guidText)
        {
            var guid = new Guid(guidText);
            return Encode(guid);
        }

        public static string Encode(Guid guid)
        {
            var enc = Convert.ToBase64String(guid.ToByteArray());
            enc = enc.Replace("/", "_");
            enc = enc.Replace("+", "-");
            return enc.Substring(0, 22);
        }

        public static Guid Decode(string encoded)
        {
            encoded = encoded.Replace("_", "/");
            encoded = encoded.Replace("-", "+");
            var buffer = Convert.FromBase64String(encoded + "==");
            return new Guid(buffer);
        }
    }

    internal class DeterministicGuid
    {
        public Guid NameSpace;
        private readonly byte[] _namespaceBytes;

        public DeterministicGuid(Guid guidNameSpace)
        {
            NameSpace = guidNameSpace;
            _namespaceBytes = guidNameSpace.ToByteArray();
            SwapByteOrder(_namespaceBytes);
        }

        public Guid Create(byte[] input)
        {
            byte[] hash;
            using (var algorithm = SHA1.Create())
            {
                algorithm.TransformBlock(_namespaceBytes, 0, _namespaceBytes.Length, null, 0);
                algorithm.TransformFinalBlock(input, 0, input.Length);
                hash = algorithm.Hash;
            }

            var newGuid = new byte[16];
            Array.Copy(hash, 0, newGuid, 0, 16);

            newGuid[6] = (byte)((newGuid[6] & 0x0F) | (5 << 4));
            newGuid[8] = (byte)((newGuid[8] & 0x3F) | 0x80);

            SwapByteOrder(newGuid);
            return new Guid(newGuid);
        }

        private static void SwapByteOrder(byte[] guid)
        {
            SwapBytes(guid, 0, 3);
            SwapBytes(guid, 1, 2);
            SwapBytes(guid, 4, 5);
            SwapBytes(guid, 6, 7);
        }

        private static void SwapBytes(byte[] guid, int left, int right)
        {
            var temp = guid[left];
            guid[left] = guid[right];
            guid[right] = temp;
        }
    }

    internal static class ExtensionsToDeterministicGuid
    {
        public static Guid Create(this DeterministicGuid source, Guid input)
        {
            return source.Create(input.ToByteArray());
        }

        public static Guid Create(this DeterministicGuid source, string input)
        {
            return source.Create(Encoding.UTF8.GetBytes(input));
        }

        public static Guid Create(this DeterministicGuid source, int input)
        {
            return source.Create(BitConverter.GetBytes(input));
        }
    }
}