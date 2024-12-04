using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using JorJika.EventBus.Abstractions;
using JorJika.EventBus.RabbitMQ.WebApp1.IntegrationEvents.EventHandling;
using JorJika.EventBus.RabbitMQ.WebApp1.IntegrationEvents.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RabbitMQ.Client;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Serilog.Sinks.Elasticsearch;

namespace JorJika.EventBus.RabbitMQ.WebApp1
{
    public class Startup
    {

        public const string UpStreamPath = "/webapp1";
        public const string v1 = "version";
        public const string ApplicationName = "PBG.WebApp1-Test";

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            var loggingURL = "http://test.elklog.bank.ge:9200";

            // Create Serilog Elasticsearch logger
            Log.Logger = new LoggerConfiguration()
               //.Enrich.With<MachineNameEnricher>()
               .Enrich.WithProperty("Version", nameof(v1))
               .Enrich.WithProperty("Application", ApplicationName)
               .Enrich.WithProperty("HostName", Environment.MachineName)
               .Enrich
               .FromLogContext()
               .MinimumLevel.Warning()
               .MinimumLevel.Override("PBG", LogEventLevel.Information)
               .MinimumLevel.Override("JorJika", LogEventLevel.Information)
               .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(loggingURL))
               {
                   //MinimumLogEventLevel = LogEventLevel.Warning,
                   AutoRegisterTemplate = true,
                   IndexFormat = "pbg-transfers-{0:yyyy.MM.dd}"
               })
               .CreateLogger();

            services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));

            services.AddMvc().AddJsonOptions(x =>
            {
                x.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                x.SerializerSettings.Formatting = Formatting.Indented;
                x.SerializerSettings.ContractResolver = new DefaultContractResolver();
            }).AddXmlSerializerFormatters()
           .SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddFilter("Microsoft", LogLevel.Warning);
                builder.AddFilter("System", LogLevel.Error);
                builder.AddFilter("Engine", LogLevel.Warning);
            });

            services.AddSingleton<IRabbitMQPersistentConnection>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<DefaultRabbitMQPersistentConnection>>();
                var factory = new ConnectionFactory() { HostName = "test.eventbus.bank.ge" }; //linux.jorjika.net
                factory.UserName = "user1";
                factory.Password = "test123";
                //factory.UserName = "user";
                //factory.Password = "user";

                return new DefaultRabbitMQPersistentConnection(factory, logger);
            });

            RegisterEventBus(services);

            var container = new ContainerBuilder();
            container.Populate(services);
            return new AutofacServiceProvider(container.Build());
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IApplicationLifetime lifetime, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto });

            app.Use(async (ctx, next) =>
            {
                var remoteIp = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault() ?? ctx.Connection.RemoteIpAddress.ToString();
                using (LogContext.PushProperty("RemoteIPAddress", remoteIp))
                {
                    await next();
                }
            });

            app.UsePathBase(UpStreamPath);

            app.Use((context, next) =>
            {
                context.Request.PathBase = UpStreamPath;
                return next();
            });


            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStaticFiles();
            app.UseMvc();

            

            ConfigureEventBus(app);
        }

        private void RegisterEventBus(IServiceCollection services)
        {
            var subscriptionClientName = "PBG.WebApp1-Test";

            services.AddSingleton<IEventBus, EventBusRabbitMQ>(sp =>
            {
                var rabbitMQPersistentConnection = sp.GetRequiredService<IRabbitMQPersistentConnection>();
                var iLifetimeScope = sp.GetRequiredService<ILifetimeScope>();
                var logger = sp.GetRequiredService<ILogger<EventBusRabbitMQ>>();
                var eventBusSubcriptionsManager = sp.GetRequiredService<IEventBusSubscriptionsManager>();

                var retryCount = 5;
                if (!string.IsNullOrEmpty(Configuration["EventBusRetryCount"]))
                {
                    retryCount = int.Parse(Configuration["EventBusRetryCount"]);
                }

                return new EventBusRabbitMQ(rabbitMQPersistentConnection, logger, iLifetimeScope, eventBusSubcriptionsManager, subscriptionClientName, retryCount);
            });

            services.AddSingleton<IEventBusSubscriptionsManager, InMemoryEventBusSubscriptionsManager>();
            services.AddTransient<CustomerCreatedIntegrationEventHandler>();
        }

        protected virtual void ConfigureEventBus(IApplicationBuilder app)
        {
            var eventBus = app.ApplicationServices.GetRequiredService<IEventBus>();
            eventBus.Subscribe<CustomerCreatedIntegrationEvent, CustomerCreatedIntegrationEventHandler>();

            eventBus.StartReceivingEvents();
        }
    }
}
