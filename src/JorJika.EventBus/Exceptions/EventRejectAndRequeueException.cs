using System;
using System.Collections.Generic;
using System.Text;

namespace JorJika.EventBus.Exceptions
{
    public class EventRejectAndRequeueException : Exception
    {
        public EventRejectAndRequeueException(string message) : base(message)
        {
        }
    }
}
