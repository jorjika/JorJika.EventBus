﻿using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;

namespace JorJika.EventBus.RabbitMQ
{
    public interface IRabbitMQPersistentConnection : IDisposable
    {
        bool IsConnected { get; }

        bool TryConnect();

        IModel CreateModel();
    }
}
