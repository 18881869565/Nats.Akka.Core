核心注入：
```c#
hostBuilder.ConfigureServices((context, services) =>
{
    services.AddSingleton<NatsClientFactory>();

    var natsUrl = configuration["Nats:Url"];
    // 注册IConnection为单例
    services.AddSingleton<IConnection>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<Program>>();
        var factory = sp.GetRequiredService<NatsClientFactory>(); 
        logger.LogInformation($"Nats服务地址:{natsUrl}");
        logger.LogInformation($"Nats客户端名称:{ClientName}");
        var connection = factory.CreateClient(natsUrl, ClientName);
        logger.LogInformation($"客户端{ClientName}连接成功");
        return connection;
    });

    services.AddSingleton<ModuleService>();
    services.AddSingleton<LidarBusiness>(); 
    services.AddSingleton<IModuleDevice, LidarDevice>();

    services.AddHostedService<ModuleHostService>();

});
```
建立订阅nats的接收数据类，继承NatsTopicManager
```c#
    public class NatsClientService : NatsTopicManager
    {
        private readonly IConfiguration _configuration;
        private readonly IEventAggregator _eventAggregator;
        public NatsClientService(IConnection connection, IConfiguration configuration, IEventAggregator eventAggregator) : base(connection)
        {
            _configuration = configuration;
            _eventAggregator = eventAggregator;
            ReceiveReaady();
        }

        private void ReceiveReaady()
        {
            NatsSub<MessageLidarPoints>(msg =>
            {
                var natsUrl = _configuration["Nats:Url"];
                _eventAggregator.GetEvent<LidarPointsEvent>().Publish(msg);
            });
        }
        ~NatsClientService()
        {
            
        }
    }
```
