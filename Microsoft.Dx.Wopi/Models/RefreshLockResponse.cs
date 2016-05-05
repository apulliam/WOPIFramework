
using System;
using System.Net;
using System.Net.Http;

namespace Microsoft.Dx.Wopi.Models
{
    public class RefreshLockResponse : WopiResponse
    {
        internal RefreshLockResponse()
        {
        }
        
        public string Lock { get; internal set; }
        public string LockFailureReason { get; internal set; }
        public string ItemVersion { get; set; }

        public override HttpResponseMessage ToHttpResponse()
        {
            var httpResponseMessage = base.ToHttpResponse();
            SetHttpResponseHeader(httpResponseMessage, WopiResponseHeaders.LOCK, Lock);
            SetHttpResponseHeader(httpResponseMessage, WopiResponseHeaders.LOCK_FAILURE_REASON, LockFailureReason);
            SetHttpResponseHeader(httpResponseMessage, WopiResponseHeaders.ITEM_VERSION, ItemVersion);
            return httpResponseMessage;
        }
    }
}
