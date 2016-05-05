using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Dx.Wopi.Models
{
    internal interface IWopiHostCapabilities
    {
        bool SupportsCobalt { get; set; }
        bool SupportsContainers { get; set; }
        bool SupportsDeleteFile { get; set; }
        bool SupportsEcosystem { get; set; }
        bool SupportsExtendedLockLength { get; set; }
        bool SupportsFolders { get; set; }
        bool SupportsGetLock { get; set; }
        bool SupportsLocks { get; set; }
        bool SupportsRename { get; set; }
        bool SupportsUpdate { get; set; }
        bool SupportsUserInfo { get; set; }

    }
}
