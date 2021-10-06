using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace JorJika.EventBus.Events
{
    public class IntegrationEvent
    {

        #region Constructor

        public IntegrationEvent()
        {
            EventId = Guid.NewGuid().ToString();
            CreationDate = DateTime.UtcNow;

            if (SourceParams == null)
                SourceParams = new SourceParameters();

            if (SourceParams.AdditionalDetails == null)
                SourceParams.AdditionalDetails = new Dictionary<string, object>();
        }

        public IntegrationEvent WithEventCorrelationId(Guid eventCorrelationId)
        {
            EventCorrelationId = eventCorrelationId.ToString();
            return this;
        }
        public IntegrationEvent WithEventCorrelationId(string eventCorrelationId)
        {
            EventCorrelationId = eventCorrelationId;
            return this;
        }

        public IntegrationEvent WithSourceApplication(string sourceApplication)
        {
            SourceParams.SourceApplication = sourceApplication;
            return this;
        }

        public IntegrationEvent WithSourceIp(string sourceIp)
        {
            SourceParams.SourceIp = sourceIp;
            return this;
        }

        public IntegrationEvent WithUsername(string username)
        {
            SourceParams.Username = username;
            return this;
        }

        public IntegrationEvent WithUserId(string userId)
        {
            SourceParams.UserId = userId;
            return this;
        }

        public IntegrationEvent WithAdditionalDetail(string key, object value)
        {
            SourceParams.AdditionalDetails.Add(key, value);
            return this;
        }

        #endregion

        #region Properties

        [JsonProperty()]
        public string EventId { get; protected set; }

        [JsonProperty()]
        public string EventCorrelationId { get; protected set; }

        [JsonProperty()]
        public DateTime CreationDate { get; protected set; }
        public SourceParameters SourceParams { get; set; }

        #endregion
    }
}
