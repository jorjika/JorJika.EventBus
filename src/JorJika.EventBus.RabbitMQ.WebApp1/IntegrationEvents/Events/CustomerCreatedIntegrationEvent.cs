using JorJika.EventBus.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace JorJika.EventBus.RabbitMQ.WebApp1.IntegrationEvents.Events
{
    public class CustomerCreatedIntegrationEvent : IntegrationEvent
    {
        public long CustomerId { get; set; }
        public string Customer { get; set; }

        public CustomerCreatedIntegrationEvent() : base()
        {
        }
    }
}
