using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Microsoft.Dx.WopiServerSql.Repository
{
    public class FileNameUtil
    {
        private static string invalidChars = System.Text.RegularExpressions.Regex.Escape(new string(System.IO.Path.GetInvalidFileNameChars()));
        private static string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);

        public static bool IsValidFileName(string fileName)
        {
            return !Regex.IsMatch(fileName, invalidRegStr);
        }

        public static string MakeValidFileName(string fileName)
        {
            return Regex.Replace(fileName, invalidRegStr, "_");
        }
    }
}
