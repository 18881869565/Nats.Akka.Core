using NATS.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nats.Akka.Core.Factory
{
    public class NatsClientFactory
    {
        public IConnection CreateClient(string serverUrl, string clientName, bool reconnectOnConnect = false)
        {
            ConnectionFactory factory = new ConnectionFactory();
            var options = ConnectionFactory.GetDefaultOptions();
            options.Url = serverUrl;
            options.Name = clientName;
            return factory.CreateConnection(options, reconnectOnConnect);
        }
    }
}
