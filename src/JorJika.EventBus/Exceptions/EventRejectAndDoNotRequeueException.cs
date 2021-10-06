using System;
using System.Collections.Generic;
using System.Text;

namespace JorJika.EventBus.Exceptions
{
    public class EventRejectAndDoNotRequeueException : Exception
    {
        public EventRejectAndDoNotRequeueException(string message) : base(message)
        {
        }
    }
}
