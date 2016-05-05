using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Dx.Wopi.Models
{
    internal interface IFileUrlProperties
    {
        Uri CloseUrl { get; set; }
        Uri DownloadUrl { get; set; }
        Uri FileSharingUrl { get; set; }
        Uri HostEditUrl { get; set; }
        Uri HostViewUrl { get; set; }
        Uri SignoutUrl { get; set; }
    }
}
