using System;
using System.Collections.Generic;
using System.Text;

namespace JorJika.EventBus.Exceptions
{
    public class EventNackAndDoNotRequeueException : Exception
    {
        public EventNackAndDoNotRequeueException(string message) : base(message)
        {
        }
    }
}
