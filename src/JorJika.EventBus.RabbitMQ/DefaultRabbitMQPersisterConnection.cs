using Polly;
using Polly.Retry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System;
using System.IO;
using System.Net.Sockets;

#if (NETCOREAPP2_0 || NETCOREAPP2_1 || NETSTANDARD2_0 || NET461)
using Microsoft.Extensions.Logging;
#endif

namespace JorJika.EventBus.RabbitMQ
{
    public class DefaultRabbitMQPersistentConnection : IRabbitMQPersistentConnection
    {
        private readonly IConnectionFactory _connectionFactory;
#if (NETCOREAPP2_0 || NETCOREAPP2_1 || NETSTANDARD2_0 || NET461)
        private readonly ILogger<DefaultRabbitMQPersistentConnection> _logger;
#endif
        private readonly int _retryCount;
        IConnection _connection;
        bool _disposed;

        object sync_root = new object();

#if (NETCOREAPP2_0 || NETCOREAPP2_1 || NETSTANDARD2_0 || NET461)
        public DefaultRabbitMQPersistentConnection(IConnectionFactory connectionFactory, ILogger<DefaultRabbitMQPersistentConnection> logger, int retryCount = 5)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _retryCount = retryCount;
        }
#endif

#if NET451
        public DefaultRabbitMQPersistentConnection(IConnectionFactory connectionFactory, int retryCount = 5)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _retryCount = retryCount;
        }
#endif

        public bool IsConnected
        {
            get
            {
                return _connection != null && _connection.IsOpen && !_disposed;
            }
        }

        public IModel CreateModel()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("No RabbitMQ connections are available to perform this action");
            }

            return _connection.CreateModel();
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            try
            {
                _connection.Dispose();
            }
            catch (IOException ex)
            {
                LogCritical(ex.ToString());
#if NET451
                throw;
#endif
            }
        }

        public bool TryConnect()
        {
            LogInformation("RabbitMQ Client is trying to connect");

            lock (sync_root)
            {
                var retryAttemptI = 0;

                var policy = Policy.Handle<SocketException>()
                    .Or<BrokerUnreachableException>()
                    .WaitAndRetry(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(1, retryAttempt)), (ex, time) =>
                    {
                        retryAttemptI++;
                        if (retryAttemptI <= _retryCount)
                            LogWarning($"Problem connecting RabbitMQ instance. Retrying {retryAttemptI}/{_retryCount}");
                    }
                );

                policy.Execute(() => { _connection = _connectionFactory.CreateConnection(); });

                if (IsConnected)
                {
                    _connection.ConnectionShutdown += OnConnectionShutdown;
                    _connection.CallbackException += OnCallbackException;
                    _connection.ConnectionBlocked += OnConnectionBlocked;

                    LogInformation($"RabbitMQ persistent connection acquired a connection {_connection.Endpoint.HostName} and is subscribed to failure events");

                    return true;
                }
                else
                {
                    LogCritical("FATAL ERROR: RabbitMQ connections could not be created and opened");
                    return false;
                }
            }
        }

        private void OnConnectionBlocked(object sender, ConnectionBlockedEventArgs e)
        {
            if (_disposed) return;

            LogWarning("A RabbitMQ connection is shutdown. Trying to re-connect...");

            TryConnect();
        }

        void OnCallbackException(object sender, CallbackExceptionEventArgs e)
        {
            if (_disposed) return;

            LogWarning("A RabbitMQ connection throw exception. Trying to re-connect...");

            TryConnect();
        }

        void OnConnectionShutdown(object sender, ShutdownEventArgs reason)
        {
            if (_disposed) return;

            LogWarning("A RabbitMQ connection is on shutdown. Trying to re-connect...");

            TryConnect();
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

        public void LogCritical(string message)
        {

#if (NETCOREAPP2_0 || NETCOREAPP2_1 || NETSTANDARD2_0 || NET461)
            _logger.LogCritical(message);
#endif

        }

        public void LogWarning(string message, params object[] args)
        {

#if (NETCOREAPP2_0 || NETCOREAPP2_1 || NETSTANDARD2_0 || NET461)
            _logger.LogWarning(message, args);
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
