using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Dx.Wopi.Models
{
    public interface IUserPermissions
    {
        bool ReadOnly { get; set; }
        bool RestrictedWebViewOnly { get; set; }
        bool UserCanAttend { get; set; }
        bool UserCanNotWriteRelative { get; set; }
        bool UserCanPresent { get; set; }
        bool UserCanRename { get; set; }
        bool UserCanWrite { get; set; }
        bool WebEditingDisabled { get; set; }
        bool DisablePrint { get; set; }
        bool DisableTranslation { get; set; }
    }
}
