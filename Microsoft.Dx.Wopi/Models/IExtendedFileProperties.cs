using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Dx.Wopi.Models
{
    interface IExtendedFileProperties
    {
        bool DisablePrint { get; set; }
        bool DisableTranslation { get; set; }
        string FileExtension { get; set; }
        int FileNameMaxLength { get; set; }
        string LastModifiedTime { get; set; }
        string SHA256 { get; set; }
        string UniqueContentId { get; set; }

    }
}
