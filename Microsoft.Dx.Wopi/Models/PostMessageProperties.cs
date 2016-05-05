using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Dx.Wopi.Models
{
    public class PostMessageProperties : IPostMessageProperties
    {
        public bool ClosePostMessage { get; set; }
        public bool EditModePostMessage { get; set; }
        public bool EditNotificationPostMessage { get; set; }
        public bool FileSharingPostMessage { get; set; }
        public string PostMessageOrigin { get; set; }
        internal PostMessageProperties()
        {
        }
        internal PostMessageProperties Clone()
        {
            return (PostMessageProperties)this.MemberwiseClone();
        }
    }
}
