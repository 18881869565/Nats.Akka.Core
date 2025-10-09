    appsettings.json中配置地址：
      "Nats": {
      "Url": "nats://127.0.0.1:4222",
      "DeviceName": "test",
      "DeviceDescription": "test模块"
    },
    
    依赖注入
    services.AddSingleton<NatsClientFactory>();
    var natsUrl = configuration["Nats:Url"];
    var deviceName = configuration["Nats:DeviceName"];
    // 注册IConnection为单例
    services.AddSingleton<IConnection>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<Program>>();
        var factory = sp.GetRequiredService<NatsClientFactory>();
        //如果为true则等待连接成功后再返回连接对象
        var connection = factory.CreateClient(natsUrl, deviceName, true); 
        return connection;
    });

    获取对象：
    public class CarBusiness : NatsTopicManager
    {
       private readonly IConnection _connection; 
       public Business(IConnection connection) : base(connection)
       {
           _connection = connection; 
           BeginReceiveMsg();
       }
      private void BeginReceiveMsg()
      { 
            //sub
            NatsSub<MessageTest1>(msg =>
            {
                 
            });

            //Pub
            _connection.Publish(new MessageTest1());

            //Response
            NatsSubRequest<MessagesRequest1>((nats, msg) =>
             {
                 try
                 {
                     nats.Responsed(new MessagesRequest1Result());
                 }
                 catch (Exception ex)
                 {
                      
                 }
             });

             //Request,默认超时5000ms
            try
            {
               MessagesRequest1Result result = _connection.Request<MessagesRequest1, MessagesRequest1Result>(new MessagesRequest1());
            }
            catch (Exception ex)
            {
                      
            }
           
            
      }
     }
 
     
