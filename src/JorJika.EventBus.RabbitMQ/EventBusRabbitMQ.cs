using Autofac;
using JorJika.EventBus.Abstractions;
using JorJika.EventBus.Events;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Autofac.Core;
using System.Diagnostics;
using JorJika.EventBus.Exceptions;
using Serilog;
using Serilog.Context;
using JorJika.EventBus.RabbitMQ.LogEnricher;

#if (NETCOREAPP2_0 || NETCOREAPP2_1 || NETSTANDARD2_0 || NET461)
using Microsoft.Extensions.Logging;
#endif

namespace JorJika.EventBus.RabbitMQ
{
    public class EventBusRabbitMQ : IEventBus, IDisposable
    {
        private readonly string BROKER_NAME;
        private readonly string EXCHANGE_TYPE;
        public readonly IDictionary<string, object> ARGUMENTS;

        private readonly IRabbitMQPersistentConnection _persistentConnection;

#if (NETCOREAPP2_0 || NETCOREAPP2_1 || NETSTANDARD2_0 || NET461)
        private readonly ILogger<EventBusRabbitMQ> _logger;
#endif
        private readonly IEventBusSubscriptionsManager _subsManager;
        private readonly ILifetimeScope _autofac;
        private readonly string AUTOFAC_SCOPE_NAME;
        private readonly int _retryCount;

        private IModel _consumerChannel;
        private string _queueName;
        private EventingBasicConsumer _consumer;


#if (NETCOREAPP2_0 || NETCOREAPP2_1 || NETSTANDARD2_0 || NET461)
        public EventBusRabbitMQ(IRabbitMQPersistentConnection persistentConnection, ILogger<EventBusRabbitMQ> logger,
            ILifetimeScope autofac, IEventBusSubscriptionsManager subsManager, string queueName = null, int retryCount = 5, string brokerName = "DefaultEventBus")
        {
            BROKER_NAME = brokerName;
            AUTOFAC_SCOPE_NAME = brokerName;
            EXCHANGE_TYPE = "direct";
            ARGUMENTS = null;

            _persistentConnection = persistentConnection ?? throw new ArgumentNullException(nameof(persistentConnection));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _subsManager = subsManager ?? new InMemoryEventBusSubscriptionsManager();
            _queueName = queueName;
            _consumerChannel = CreateConsumerChannel();
            _autofac = autofac;
            _retryCount = retryCount;
            _subsManager.OnEventRemoved += SubsManager_OnEventRemoved;
        }

        public EventBusRabbitMQ(IRabbitMQPersistentConnection persistentConnection, ILogger<EventBusRabbitMQ> logger,
          ILifetimeScope autofac, IEventBusSubscriptionsManager subsManager, string queueName, int retryCount, string brokerName, string exchangeType, IDictionary<string, object> arguments)
        {
            BROKER_NAME = brokerName;
            AUTOFAC_SCOPE_NAME = brokerName;
            EXCHANGE_TYPE = exchangeType;
            ARGUMENTS = arguments;

            _persistentConnection = persistentConnection ?? throw new ArgumentNullException(nameof(persistentConnection));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _subsManager = subsManager ?? new InMemoryEventBusSubscriptionsManager();
            _queueName = queueName;
            _consumerChannel = CreateConsumerChannel();
            _autofac = autofac;
            _retryCount = retryCount;
            _subsManager.OnEventRemoved += SubsManager_OnEventRemoved;
        }
#endif

#if (NET451)
        public EventBusRabbitMQ(IRabbitMQPersistentConnection persistentConnection, ILifetimeScope autofac, IEventBusSubscriptionsManager subsManager, string queueName = null, int retryCount = 5, string brokerName = "DefaultEventBus")
        {
            BROKER_NAME = brokerName;
            AUTOFAC_SCOPE_NAME = brokerName;
            EXCHANGE_TYPE = "direct";
            ARGUMENTS = null;

            _persistentConnection = persistentConnection ?? throw new ArgumentNullException(nameof(persistentConnection));
            _subsManager = subsManager ?? new InMemoryEventBusSubscriptionsManager();
            _queueName = queueName;
            _consumerChannel = CreateConsumerChannel();
            _autofac = autofac;
            _retryCount = retryCount;
            _subsManager.OnEventRemoved += SubsManager_OnEventRemoved;
        }
#endif

        private void SubsManager_OnEventRemoved(object sender, string eventName)
        {
            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }

            using (var channel = _persistentConnection.CreateModel())
            {
                channel.QueueUnbind(queue: _queueName,
                    exchange: BROKER_NAME,
                    routingKey: eventName);

                if (_subsManager.IsEmpty)
                {
                    _queueName = string.Empty;
                    _consumerChannel?.Close();
                }
            }
        }

        public void Publish(IntegrationEvent @event)
        {
            var eventName = @event.GetType().Name;

            //assigning source application name
            @event.WithSourceApplication(_queueName);

            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }

            var message = JsonConvert.SerializeObject(@event);
            var body = Encoding.UTF8.GetBytes(message);

            using (LogContext.PushProperty("EventData", message))
            using (LogContext.PushProperty("EventName", eventName))
            using (LogContext.PushProperties(new[] { new IntegrationEventEnricher(@event) }))
            {
                var retryAttemptI = 0;
                var policy = Policy.Handle<BrokerUnreachableException>()
                    .Or<SocketException>()
                    .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                    {
                        retryAttemptI++;
                        if (retryAttemptI <= _retryCount)
                            LogWarning(ex, $"Problem publishing {eventName} with EventId={@event.EventId}. Retrying {retryAttemptI}/{_retryCount}");
                    });

                using (var channel = _persistentConnection.CreateModel())
                {
                    channel.ExchangeDeclare(exchange: BROKER_NAME, type: EXCHANGE_TYPE, durable: true, arguments: ARGUMENTS);

                    try
                    {
                        policy.Execute(() =>
                        {
                            var properties = channel.CreateBasicProperties();
                            properties.DeliveryMode = 2; // persistent

                        channel.BasicPublish(exchange: BROKER_NAME,
                                             routingKey: eventName,
                                             mandatory: true,
                                             basicProperties: properties,
                                             body: body);

                            LogInformation($"Published {eventName} with EventId={@event.EventId}");
                        });
                    }
                    catch (Exception ex)
                    {
                        LogCritical(ex, $"Error publishing {eventName} with EventId={@event.EventId}. Tried {_retryCount} times.");
                        throw;
                    }
                }
            }
        }

        public void SubscribeDynamic<TH>(string eventName)
            where TH : IDynamicIntegrationEventHandler
        {
            DoInternalSubscription(eventName);
            _subsManager.AddDynamicSubscription<TH>(eventName);
        }

        public void Subscribe<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = _subsManager.GetEventKey<T>();
            DoInternalSubscription(eventName);
            _subsManager.AddSubscription<T, TH>();
        }

        private void DoInternalSubscription(string eventName)
        {
            var containsKey = _subsManager.HasSubscriptionsForEvent(eventName);
            if (!containsKey)
            {
                if (!_persistentConnection.IsConnected)
                {
                    _persistentConnection.TryConnect();
                }

                using (var channel = _persistentConnection.CreateModel())
                {
                    channel.QueueBind(queue: _queueName,
                                      exchange: BROKER_NAME,
                                      routingKey: eventName);
                }
            }
        }

        public void Unsubscribe<T, TH>()
            where TH : IIntegrationEventHandler<T>
            where T : IntegrationEvent
        {
            _subsManager.RemoveSubscription<T, TH>();
        }

        public void UnsubscribeDynamic<TH>(string eventName)
            where TH : IDynamicIntegrationEventHandler
        {
            _subsManager.RemoveDynamicSubscription<TH>(eventName);
        }

        public void Dispose()
        {
            if (_consumerChannel != null)
            {
                _consumerChannel.Dispose();
            }

            _subsManager.Clear();
        }

        private IModel CreateConsumerChannel()
        {
            if (!_persistentConnection.IsConnected)
            {
                _persistentConnection.TryConnect();
            }

            var channel = _persistentConnection.CreateModel();

            channel.ExchangeDeclare(exchange: BROKER_NAME,
                                 type: EXCHANGE_TYPE,
                                 durable: true,
                                 arguments: ARGUMENTS);

            channel.QueueDeclare(queue: _queueName,
                                 durable: true,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);


            _consumer = new EventingBasicConsumer(channel);
            _consumer.Received += async (model, ea) =>
            {

                var eventName = ea.RoutingKey;
                var eventData = Encoding.UTF8.GetString(ea.Body);
                IntegrationEvent eventBase = null;

                try
                {
                    eventBase = JsonConvert.DeserializeObject<IntegrationEvent>(eventData);
                }
                catch
                {
                    //ignored
                }

                using (LogContext.PushProperty("EventData", eventData))
                using (LogContext.PushProperty("EventName", eventName))
                using (LogContext.PushProperties(new[] { new IntegrationEventEnricher(eventBase) }))
                    try
                    {
                        await ProcessEvent(eventName, eventData, eventBase);
                        channel.BasicAck(ea.DeliveryTag, multiple: false);
                    }
                    catch (EventNackAndRequeueException ex)
                    {
                        channel.BasicNack(ea.DeliveryTag, false, true);
                        LogWarning(ex, $"Event {eventName} not acked but requeued again.");
                    }
                    catch (EventNackAndDoNotRequeueException ex)
                    {
                        channel.BasicNack(ea.DeliveryTag, false, false);
                        LogWarning(ex, $"Event {eventName} not acked and not requeued.");
                    }
                    catch (EventRejectAndRequeueException ex)
                    {
                        channel.BasicReject(ea.DeliveryTag, true);
                        LogWarning(ex, $"Event {eventName} rejected but requeued again.");
                    }
                    catch (EventRejectAndDoNotRequeueException ex)
                    {
                        channel.BasicReject(ea.DeliveryTag, false);
                        LogWarning(ex, $"Event {eventName} rejected and not requeued.");
                    }
                    catch (Exception ex)
                    {
                        channel.BasicReject(ea.DeliveryTag, false);
                        LogCritical(ex, $"Event {eventName} rejected and not requeued.");
                    }

            };

            channel.CallbackException += (sender, ea) =>
            {
                _consumerChannel.Dispose();
                _consumerChannel = CreateConsumerChannel();
            };

            return channel;
        }

        public void StartReceivingEvents()
        {
            _consumerChannel.BasicConsume(queue: _queueName, autoAck: false, consumer: _consumer);
        }

        private async Task ProcessEvent(string eventName, string message, IntegrationEvent eventBase)
        {
            if (_subsManager.HasSubscriptionsForEvent(eventName))
            {
                var sw = new Stopwatch();
                sw.Start();

                var eventIdMsg = "";

                if (eventBase != null)
                    eventIdMsg = $" with EventId = { eventBase.EventId }";

                LogInformation($"Handling {eventName}{eventIdMsg} ...");

                try
                {
                    using (var scope = _autofac.BeginLifetimeScope(AUTOFAC_SCOPE_NAME))
                    {
                        var subscriptions = _subsManager.GetHandlersForEvent(eventName);
                        foreach (var subscription in subscriptions)
                        {
                            if (subscription.IsDynamic)
                            {
                                var handler = scope.ResolveOptional(subscription.HandlerType) as IDynamicIntegrationEventHandler;
                                dynamic eventData = JObject.Parse(message);
                                await handler.Handle(eventData);
                            }
                            else
                            {
                                var eventType = _subsManager.GetEventTypeByName(eventName);
                                var integrationEvent = JsonConvert.DeserializeObject(message, eventType);
                                var handler = scope.ResolveOptional(subscription.HandlerType);
                                var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
                                await (Task)concreteType.GetMethod("Handle").Invoke(handler, new object[] { integrationEvent });
                            }
                        }
                    }

                    LogInformation($"Event {eventName}{eventIdMsg} handled successfully. Processing Time: {sw.ElapsedMilliseconds}ms");

                    sw.Stop();
                }
                catch (JsonReaderException jrEx)
                {
                    LogCritical(jrEx, $"Error handling {eventName}{eventIdMsg}. JObject Parsing error. Processing Time: {sw.ElapsedMilliseconds}ms");
                    throw;
                }
                catch (DependencyResolutionException drEx)
                {
                    LogCritical(drEx, $"Error handling {eventName}{eventIdMsg}. Autofac ResolveOptional method error. Processing Time: {sw.ElapsedMilliseconds}ms");
                    throw;
                }
                catch (Exception ex)
                {
                    LogError(ex, $"Error handling {eventName}{eventIdMsg}. Processing Time: {sw.ElapsedMilliseconds}ms");
                    throw;
                }
            }
            else
            {
                var queue = _queueName;
                var eventData = message;
                LogWarning("Queue {queue} is not subscribed to {eventName}.", queue, eventName);
            }
        }



        public void LogInformation(string message)
        {

#if (NETCOREAPP2_0 || NETCOREAPP2_1 || NETSTANDARD2_0 || NET461)
            _logger.LogInformation(message);
#endif

        }

        public void LogCritical(Exception ex, string message)
        {

#if (NETCOREAPP2_0 || NETCOREAPP2_1 || NETSTANDARD2_0 || NET461)
            _logger.LogCritical(ex, message);
#endif

        }

        public void LogWarning(string message, params object[] args)
        {

#if (NETCOREAPP2_0 || NETCOREAPP2_1 || NETSTANDARD2_0 || NET461)
            _logger.LogWarning(message, args);
#endif

        }


        public void LogWarning(Exception ex, string message, params object[] args)
        {

#if (NETCOREAPP2_0 || NETCOREAPP2_1 || NETSTANDARD2_0 || NET461)
            _logger.LogWarning(ex, message, args);
#endif

        }

        public void LogError(Exception ex, string message, params object[] args)
        {

#if (NETCOREAPP2_0 || NETCOREAPP2_1 || NETSTANDARD2_0 || NET461)
            _logger.LogError(ex, message, args);
#endif

        }

        public void LogWarning(Exception ex, string message)
        {

#if (NETCOREAPP2_0 || NETCOREAPP2_1 || NETSTANDARD2_0 || NET461)
            _logger.LogWarning(ex, message);
#endif

        }

    }
}
