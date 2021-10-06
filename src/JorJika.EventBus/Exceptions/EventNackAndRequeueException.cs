using System;
using System.Collections.Generic;
using System.Text;

namespace JorJika.EventBus.Exceptions
{
    public class EventNackAndRequeueException : Exception
    {
        public EventNackAndRequeueException(string message) : base(message)
        {
        }
    }
}
