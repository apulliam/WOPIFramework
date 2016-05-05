using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;

namespace Microsoft.Dx.Wopi.Models
{
    public class GetFileResponse : WopiResponse
    {
        internal GetFileResponse()
        {
        }

        public HttpContent Content { get; internal set; }

        public string ItemVersion { get; set; }

        public override HttpResponseMessage ToHttpResponse()
        {
            var httpResponseMessage = base.ToHttpResponse();
            if (StatusCode == HttpStatusCode.OK)
            {
                SetHttpResponseHeader(httpResponseMessage, WopiResponseHeaders.ITEM_VERSION, ItemVersion);
                httpResponseMessage.Content = Content;
            }
            return httpResponseMessage;
        }
    }
}