using NATS.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Nats.Akka.Core.Manager
{
    public abstract class NatsTopicManager : NatsTopicReceiveBase
    { 
        private readonly IConnection _connection;
        public Action<Msg> AllSubMsgAction;
        public NatsTopicManager( IConnection connection)
        {
            _connection = connection;
        }

        private void SubTopic(string topicName, Type receiveType)
        { 
            _connection.SubscribeAsync(topicName, (sender, args) =>
            {
                var subject = args.Message.Subject;
                if (TopicSubEventDic.TryGetValue(subject, out var eventHandler))
                {
                    foreach (var handler in eventHandler)
                    {
                        if (handler is Action<Msg> msgHandler)
                        {
                            // 如果是 Action<Msg> 类型的处理函数，则调用它
                            Task.Run(() => msgHandler(args.Message));
                        }
                        else if (handler is Delegate typedHandler)
                        {
                            // 处理 Action<T> 类型的委托
                            Task.Run(() =>
                            {
                                // 获取委托的参数类型，并将 Msg 转换为相应类型
                                var paramType = typedHandler.Method.GetParameters().First().ParameterType;
                                var msgObj = ConvertObjMsg(paramType, args.Message);
                                typedHandler.DynamicInvoke(msgObj);
                            });
                        }
                    }
                }
                if (AllSubMsgAction != null)
                {
                    Task.Run(() => AllSubMsgAction(args.Message));
                }
            });
        }

        protected void NatsSub<T>(Action<T> handler)
        {
            Type receiveType = typeof(T);
            var topicName = receiveType.FullName;
            if (TopicSubEventDic != null)
            {
                if (TopicSubEventDic.TryGetValue(topicName, out var actions))
                {
                    actions.Add((Action<Msg>)(msg => handler(ConvertObjMsg<T>(msg))));
                }
                else
                {
                    var msgActions = new List<Delegate>()
                    {
                        (Action<Msg>)(msg => handler(ConvertObjMsg<T>(msg)))
                    };
                    TopicSubEventDic.Add(topicName, msgActions);
                }
            }
            SubTopic(topicName, receiveType);
        }

        protected void NatsSubRequest<TRequest>(Action<Msg, TRequest> action)
        {
            Type receiveType = typeof(TRequest);
            var topicName = receiveType.FullName;
            if (TopicSubRequestEventDic != null)
            {
                if (TopicSubRequestEventDic.TryGetValue(topicName, out var actions))
                {
                    actions = action;
                }
                else
                {
                    TopicSubRequestEventDic.Add(topicName, action);
                }
            }
            SubRequestTopic(topicName, receiveType);

        }
        private void SubRequestTopic(string topicName, Type receiveType)
        {
            Console.WriteLine($"订阅Request主题  名称【{topicName}】");
            _connection.SubscribeAsync(topicName, (sender, args) =>
            {
                var subject = args.Message.Subject;
                if (TopicSubRequestEventDic.TryGetValue(subject, out var eventHandler))
                {
                    if (eventHandler is Delegate typedHandler)
                    {
                        // 处理 Action<T> 类型的委托
                        Task.Run(() =>
                        {
                            // 获取委托的参数类型，并将 Msg 转换为相应类型
                            var paramType = typedHandler.Method.GetParameters().Last().ParameterType;
                            var msgObj = ConvertObjMsg(paramType, args.Message);
                            typedHandler.DynamicInvoke(args.Message, msgObj);
                        });
                    }
                }
            });
        }
        public T ConvertObjMsg<T>(Msg msg)
        {
            // 反序列化消息
            T t = JsonSerializer.Deserialize<T>(new ReadOnlySpan<byte>(msg.Data));
            return t;
        }
        public object ConvertObjMsg(Type targetType, Msg msg)
        {
            // 通过目标类型进行反序列化
            return JsonSerializer.Deserialize(new ReadOnlySpan<byte>(msg.Data), targetType);
        }
    }
}
