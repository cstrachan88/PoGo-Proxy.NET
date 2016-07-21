using System;

namespace PoGo_Proxy
{
    public class ResponseEventArgs : EventArgs
    {
        public ulong RequestId { get; set; }
        public MessageBlock Requests { get; set; }
        public MessageBlock Responses { get; set; }
    }
}
