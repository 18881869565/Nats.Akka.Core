using NATS.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Nats.Akka.Core.Extension
{
    public static class NatsMsgExtension
    {
        public static void Responsed<T>(this Msg msg, T response) where T : class
        {
            msg.Respond(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response)));
        }
    }
}
