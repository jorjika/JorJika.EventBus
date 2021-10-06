using JorJika.EventBus.Events;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace JorJika.EventBus.RabbitMQ.LogEnricher
{
    public class IntegrationEventEnricher : ILogEventEnricher
    {
        private IntegrationEvent _integrationEvent;
        public IntegrationEventEnricher(IntegrationEvent integrationEvent)
        {
            _integrationEvent = integrationEvent;
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (_integrationEvent != null)
            {
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("_EventId", _integrationEvent.EventId));
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("EventCorrelationId", _integrationEvent.EventCorrelationId));
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("UserId", _integrationEvent.SourceParams.UserId));
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("Username", _integrationEvent.SourceParams.Username));
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("SourceIp", _integrationEvent.SourceParams.SourceIp));
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("SourceApplication", _integrationEvent.SourceParams.SourceApplication));
            }
        }
    }
}
