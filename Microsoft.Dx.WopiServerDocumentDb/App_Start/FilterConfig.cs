using System.Web;
using System.Web.Mvc;

namespace Microsoft.Dx.WopiServerDocumentDb
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }
    }
}
