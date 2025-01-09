using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nats.Akka.Core.Manager
{
    public abstract class NatsTopicReceiveBase
    {
        public Dictionary<string, List<Delegate>> TopicSubEventDic = new Dictionary<string, List<Delegate>>();
        public Dictionary<string, Delegate> TopicSubRequestEventDic = new Dictionary<string, Delegate>();

    }
}
