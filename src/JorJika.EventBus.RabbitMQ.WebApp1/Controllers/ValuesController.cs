﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JorJika.EventBus.Abstractions;
using JorJika.EventBus.RabbitMQ.WebApp1.IntegrationEvents.Events;
using Microsoft.AspNetCore.Mvc;

namespace JorJika.EventBus.RabbitMQ.WebApp1.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        private IEventBus _eventBus;
        public ValuesController(IEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        // GET api/values
        [HttpGet]
        public ActionResult<IEnumerable<string>> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/values/5
        [HttpGet("{id}")]
        public ActionResult<string> Get(string id)
        {
            _eventBus.Publish((new CustomerCreatedIntegrationEvent() { CustomerId = 1, Customer = id }).WithEventCorrelationId("asdasdasdasdasdasd"));

            return $"{id}";
        }

        // POST api/values
        [HttpPost]
        public void Post([FromBody] string value)
        {
        }

        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
