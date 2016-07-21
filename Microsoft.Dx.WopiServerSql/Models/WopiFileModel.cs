using Microsoft.Dx.Wopi;
using Microsoft.Dx.Wopi.Models;
using Microsoft.Dx.WopiServerSql.Repository;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Microsoft.Dx.WopiServerSql.Models
{
    public class WopiFileModel 
    {
        public async static Task<WopiFileModel> CreateWopiFileModel(WopiFile wopiFile)
        {
            var wopiFileModel = new WopiFileModel(wopiFile);
            await wopiFileModel.PopulateActions();
            return wopiFileModel;
        }

        private async Task PopulateActions()
        {
            // Get the discovery informations
            var actions = await WopiDiscovery.GetActions();
            var extension = FileExtension;
            if (extension.StartsWith("."))
                extension = extension.Substring(1);
            Actions = actions.Where(i => i.ext == extension).OrderBy(i => i.isDefault).ToList();
        }

        public WopiFileModel(WopiFile wopiFile)
        {
            FileId = wopiFile.FileId;
            FileName = wopiFile.FileName;
            FileExtension = wopiFile.FileExtension;
            Size = wopiFile.Size;
            Version = wopiFile.Version;
            //PopulateActions().RunSynchronously();
        }

        //[JsonProperty(PropertyName = "id")]
        [JsonProperty(PropertyName = "FileId")]
        public string FileId { get; set; }

        [JsonProperty(PropertyName = "FileName")]
        public string FileName { get; set; }

        [JsonProperty(PropertyName = "FileExtension")]
        public string FileExtension { get; set; }

        [JsonProperty(PropertyName = "Size")]
        public long Size { get; set; }

        [JsonProperty(PropertyName = "Version")]
        public int Version { get; set; }

        [JsonProperty(PropertyName = "Actions")]
        public List<WopiAction> Actions { get; set; }
    }
}