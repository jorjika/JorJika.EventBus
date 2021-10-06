using System;
using System.Collections.Generic;
using System.Text;

namespace JorJika.EventBus.Events
{
    public class SourceParameters
    {
        public string SourceIp { get; set; }
        public string UserId { get; set; }
        public string Username { get; set; }
        public string SourceApplication { get; set; }
        public Dictionary<string, object> AdditionalDetails { get; set; }
    }
}
