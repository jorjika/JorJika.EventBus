using JorJika.EventBus.Abstractions;
using JorJika.EventBus.RabbitMQ.WebApp1.IntegrationEvents.Events;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JorJika.EventBus.Exceptions;

namespace JorJika.EventBus.RabbitMQ.WebApp1.IntegrationEvents.EventHandling
{
    public class CustomerCreatedIntegrationEventHandler : IIntegrationEventHandler<CustomerCreatedIntegrationEvent>
    {
        IEventBus _eventBus;
        ILogger<DefaultRabbitMQPersistentConnection> _logger;
        public CustomerCreatedIntegrationEventHandler(ILogger<DefaultRabbitMQPersistentConnection> logger, IEventBus eventBus)
        {
            _logger = logger;
            _eventBus = eventBus;
        }

        public async Task Handle(CustomerCreatedIntegrationEvent @event)
        {
            _logger.LogInformation($"{@event.CustomerId} - {@event.Customer} - Source: {@event.SourceParams.SourceApplication}; Date: {@event.CreationDate.ToString("yyyy-MM-dd HH:mm:ss")}; EventId: {@event.EventId}");
            throw new EventRejectAndDoNotRequeueException("Hello world");
            //Console.WriteLine($"{@event.CustomerId} - {@event.Customer} - Source: {@event.SourceApp}; Date: {@event.CreationDate.ToString("yyyy-MM-dd HH:mm:ss")}; EventId: {@event.EventId}");
        }
    }
}
