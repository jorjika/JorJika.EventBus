﻿using JorJika.EventBus.Abstractions;
using JorJika.EventBus.RabbitMQ.WebApp2.IntegrationEvents.Events;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JorJika.EventBus.RabbitMQ.WebApp2.IntegrationEvents.EventHandling
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
            //_logger.LogInformation($"{@event.CustomerId} - {@event.Customer} - Source: {@event.SourceApp}; Date: {@event.CreationDate.ToString("yyyy-MM-dd HH:mm:ss")}; EventId: {@event.EventId}");
            Console.WriteLine($"{@event.CustomerId} - {@event.Customer} - Source: {@event.SourceParams.SourceApplication}; Date: {@event.CreationDate.ToString("yyyy-MM-dd HH:mm:ss")}; EventId: {@event.EventId}");
        }
    }
}
