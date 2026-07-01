using NATS.Client;
using Nats.Akka.Core.Internal;

namespace Nats.Akka.Core.Extension
{
    public static class NatsMsgExtension
    {
        public static void Responsed<T>(this Msg msg, T response)
        {
            msg.Respond(NatsMessageCodec.Serialize(response));
        }
    }
}
