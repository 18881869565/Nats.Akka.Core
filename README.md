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

Pub数据：Publish
```c#
connection.Publish(messageLidarPoints);
```

Sub数据：NatsSub
```c#
    public class LidarBusiness : NatsTopicManager
 {
     private readonly ILogger<NatsTopicManager> _logger;
     private readonly IConnection _connection;
     public LidarBusiness(ILogger<NatsTopicManager> logger, IConnection connection) : base(connection)
     {
         _logger = logger;
         _connection = connection;
         BeginReceiveMsg();
     }

     public void BeginReceiveMsg()
     {
         NatsSub<MessageLidarPoints>(msg =>
         {
             _logger.LogInformation($"{DateTime.Now.ToString()}接收Nats Sub消息");
             //var messageLidarPoints = ConvertObjMsg<MessageLidarPoints>(msg);
             //_logger.LogInformation(JsonSerializer.Serialize(messageLidarPoints));

         });

         NatsSubRequest<MessageRequset>((msg, MessageRequset) =>
         {
             _logger.LogInformation($"{DateTime.Now.ToString()}接收Nats Req消息");
             msg.Responsed(new MessageResonse(2));
         });

        
     }
 }
```
Req请求（默认超时5s）：
```c#
 var messageResonse = connection.Request<MessageRequset, MessageResonse>(new MessageRequset());
```
Respon回复：
```c#
 NatsSubRequest<MessageRequset>((msg, MessageRequset) =>
 {
     _logger.LogInformation($"{DateTime.Now.ToString()}接收Nats Req消息");
     msg.Responsed(new MessageResonse(2));
 });
```

