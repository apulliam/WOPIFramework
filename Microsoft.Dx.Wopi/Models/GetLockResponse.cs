using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;

namespace Microsoft.Dx.Wopi.Models
{
    public class GetLockResponse : WopiResponse
    {
        internal GetLockResponse()
        { }

        public string Lock { get; internal set; }
       
        public string LockFailureReason { get; set; }

        public override HttpResponseMessage ToHttpResponse()
        {
            var httpResponseMessage = base.ToHttpResponse();
            SetHttpResponseHeader(httpResponseMessage, WopiResponseHeaders.LOCK, Lock);
            SetHttpResponseHeader(httpResponseMessage, WopiResponseHeaders.LOCK_FAILURE_REASON, LockFailureReason);
            return httpResponseMessage;
        }
    }
      
}