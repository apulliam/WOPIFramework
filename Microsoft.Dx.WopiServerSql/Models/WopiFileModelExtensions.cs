using Microsoft.Dx.Wopi;
using Microsoft.Dx.WopiServerSql.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Dx.WopiServerSql.Models
{
    public static class WopiFileModelExtensions
    {

        /// <summary>
        /// Populates a file with action details from WOPI discovery based on the file extension
        /// </summary>
        public async static Task PopulateActions(this WopiFileModel model)
        {
            // Get the discovery informations
            var actions = await WopiDiscovery.GetActions();
            var extension = model.FileExtension;
            if (extension.StartsWith("."))
                extension = extension.Substring(1); 
            model.Actions = actions.Where(i => i.ext == extension).OrderBy(i => i.isDefault).ToList();
        }

        /// <summary>
        /// Populates a list of files with action details from WOPI discovery
        /// </summary>
        public async static Task PopulateActions(this IEnumerable<WopiFileModel> files)
        {
            if (files.Count() > 0)
            {
                foreach (var file in files)
                {
                    await file.PopulateActions();
                }
            }
        }

        public async static Task<WopiFileModel> ToWopiFileModel(this WopiFile wopiFile)
        {
            return await WopiFileModel.CreateWopiFileModel(wopiFile);
        }

    }
}
