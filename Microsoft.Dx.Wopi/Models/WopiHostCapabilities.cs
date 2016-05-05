using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Dx.Wopi.Models
{
    public class WopiHostCapabilities : IWopiHostCapabilities
    {
        public bool SupportsCobalt { get; set; }
        public bool SupportsContainers { get; set; }
        public bool SupportsDeleteFile { get; set; }
        public bool SupportsEcosystem { get; set; }
        public bool SupportsExtendedLockLength { get; set; }
        public bool SupportsFolders { get; set; }
        public bool SupportsGetLock { get; set; }
        public bool SupportsLocks { get; set; }
        public bool SupportsRename { get; set; }
        public bool SupportsUpdate { get; set; }
        public bool SupportsUserInfo { get; set; }

        internal WopiHostCapabilities()
        {
        }

        internal WopiHostCapabilities Clone()
        {
            return (WopiHostCapabilities)this.MemberwiseClone();
        }
    }
}
