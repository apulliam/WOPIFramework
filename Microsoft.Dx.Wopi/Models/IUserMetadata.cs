using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Dx.Wopi.Models
{
    public interface IUserMetadata
    {
        bool IsEduUser { get; set; }
        bool LicenseCheckForEditIsEnabled { get; set; }
        string UserFriendlyName { get; set; }
        string UserInfo { get; set; }
    }
}
