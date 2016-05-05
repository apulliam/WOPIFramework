using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Dx.Wopi.Models
{
    public interface IWopiHostProperties
    {
        bool AllowExternalMarketplace { get; set; }
        bool CloseButtonClosesWindow { get; set; }
        int FileNameMaxLength { get; set; }
    }
}
