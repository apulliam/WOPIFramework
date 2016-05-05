using Microsoft.Dx.Wopi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Dx.WopiServerDocumentDb.Models
{
    public static class FileModelExtensions
    {

        /// <summary>
        /// Populates a file with action details from WOPI discovery based on the file extension
        /// </summary>
        public async static Task PopulateActions(this DetailedFileModel file)
        {
            // Get the discovery informations
            var actions = await WopiDiscovery.GetActions();
            var fileExt = file.BaseFileName.Substring(file.BaseFileName.LastIndexOf('.') + 1).ToLower();
            file.Actions = actions.Where(i => i.ext == fileExt).OrderBy(i => i.isDefault).ToList();
        }

        /// <summary>
        /// Populates a list of files with action details from WOPI discovery
        /// </summary>
        public async static Task PopulateActions(this IEnumerable<DetailedFileModel> files)
        {
            if (files.Count() > 0)
            {
                foreach (var file in files)
                {
                    await file.PopulateActions();
                }
            }
        }

    }
}
