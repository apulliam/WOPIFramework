using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Threading.Tasks;
using System.Net.Http;
using System.Xml.Linq;
using System.Configuration;
using System.Runtime.Caching;
using Microsoft.Dx.Wopi.Models;
using Microsoft.Dx.Wopi.Security;

namespace Microsoft.Dx.Wopi
{
    public class WopiDiscovery
    {

        /// <summary>
        /// Contains all valid URL placeholders for different WOPI actions
        /// </summary>
        public class WopiUrlPlaceholders
        {
            public static List<string> Placeholders = new List<string>() { BUSINESS_USER,
            DC_LLCC, DISABLE_ASYNC, DISABLE_CHAT, DISABLE_BROADCAST,
            EMBDDED, FULLSCREEN, PERFSTATS, RECORDING, THEME_ID, UI_LLCC,
            VALIDATOR_TEST_CATEGORY
        };
            public const string BUSINESS_USER = "<IsLicensedUser=BUSINESS_USER&>";
            public const string DC_LLCC = "<rs=DC_LLCC&>";
            public const string DISABLE_ASYNC = "<na=DISABLE_ASYNC&>";
            public const string DISABLE_CHAT = "<dchat=DISABLE_CHAT&>";
            public const string DISABLE_BROADCAST = "<vp=DISABLE_BROADCAST&>";
            public const string EMBDDED = "<e=EMBEDDED&>";
            public const string FULLSCREEN = "<fs=FULLSCREEN&>";
            public const string PERFSTATS = "<showpagestats=PERFSTATS&>";
            public const string RECORDING = "<rec=RECORDING&>";
            public const string THEME_ID = "<thm=THEME_ID&>";
            public const string UI_LLCC = "<ui=UI_LLCC&>";
            public const string VALIDATOR_TEST_CATEGORY = "<testcategory=VALIDATOR_TEST_CATEGORY>";

            /// <summary>
            /// Sets a specific WOPI URL placeholder with the correct value
            /// Most of these are hard-coded in this WOPI implementation
            /// </summary>
            public static string GetPlaceholderValue(string placeholder)
            {
                var ph = placeholder.Substring(1, placeholder.IndexOf("="));
                string result = "";
                switch (placeholder)
                {
                    case BUSINESS_USER:
                        result = ph + "1";
                        break;
                    case DC_LLCC:
                    case UI_LLCC:
                        result = ph + "1033";
                        break;
                    case DISABLE_ASYNC:
                    case DISABLE_BROADCAST:
                    case EMBDDED:
                    case FULLSCREEN:
                    case RECORDING:
                    case THEME_ID:
                        // These are all broadcast related actions
                        result = ph + "true";
                        break;
                    case DISABLE_CHAT:
                        result = ph + "false";
                        break;
                    case PERFSTATS:
                        result = ""; // No documentation
                        break;
                    case VALIDATOR_TEST_CATEGORY:
                        result = ph + "OfficeOnline"; //This value can be set to All, OfficeOnline or OfficeNativeClient to activate tests specific to Office Online and Office for iOS. If omitted, the default value is All.  
                        break;
                    default:
                        result = "";
                        break;

                }

                return result;
            }
        }



        //WOPI protocol constants
        public const string WOPI_BASE_PATH = @"/wopi/";
        public const string WOPI_CHILDREN_PATH = @"/children";
        public const string WOPI_CONTENTS_PATH = @"/contents";
        public const string WOPI_FILES_PATH = @"files/";
        public const string WOPI_FOLDERS_PATH = @"folders/";

        /// <summary>
        /// Gets the discovery information from WOPI discovery and caches it appropriately
        /// </summary>
        public async static Task<List<WopiAction>> GetActions()
        {
            List<WopiAction> actions = new List<WopiAction>();

            // Determine if the discovery data is cached
            var memoryCache = MemoryCache.Default;
            if (!memoryCache.Contains("DiscoData"))
                await Refresh();
            if (memoryCache.Contains("DiscoData"))
                actions = (List<WopiAction>)memoryCache["DiscoData"];
            return actions;
        }

        public async static Task Refresh()
        {

            // Use the Wopi Discovery endpoint to get the data
            HttpClient client = new HttpClient();
            using (HttpResponseMessage response = await client.GetAsync(ConfigurationManager.AppSettings["WopiDiscovery"]))
            {
                if (response.IsSuccessStatusCode)
                {
                    var memoryCache = MemoryCache.Default;
                    var actions = new List<WopiAction>();

                    // Read the xml string from the response
                    var xmlString = await response.Content.ReadAsStringAsync();

                    // Parse the xml string into Xml
                    var discoXml = XDocument.Parse(xmlString);

                    // Convert the discovery xml into list of WopiApp
                    var xapps = discoXml.Descendants("app");
                    foreach (var xapp in xapps)
                    {
                        // Parse the actions for the app
                        var xactions = xapp.Descendants("action");
                        foreach (var xaction in xactions)
                        {
                            actions.Add(new WopiAction()
                            {
                                app = xapp.Attribute("name").Value,
                                favIconUrl = xapp.Attribute("favIconUrl").Value,
                                checkLicense = Convert.ToBoolean(xapp.Attribute("checkLicense").Value),
                                name = xaction.Attribute("name").Value,
                                ext = (xaction.Attribute("ext") != null) ? xaction.Attribute("ext").Value : String.Empty,
                                progid = (xaction.Attribute("progid") != null) ? xaction.Attribute("progid").Value : String.Empty,
                                isDefault = (xaction.Attribute("default") != null) ? true : false,
                                urlsrc = xaction.Attribute("urlsrc").Value,
                                requires = (xaction.Attribute("requires") != null) ? xaction.Attribute("requires").Value : String.Empty
                            });
                        }

                        // Cache the discovey data for an hour
                        memoryCache.Add("DiscoData", actions, DateTimeOffset.Now.AddHours(1));
                    }

                    // Convert the discovery xml into list of WopiApp
                    var proof = discoXml.Descendants("proof-key").FirstOrDefault();
                    var wopiProof = new WopiProof()
                    {
                        value = proof.Attribute("value").Value,
                        modulus = proof.Attribute("modulus").Value,
                        exponent = proof.Attribute("exponent").Value,
                        oldvalue = proof.Attribute("oldvalue").Value,
                        oldmodulus = proof.Attribute("oldmodulus").Value,
                        oldexponent = proof.Attribute("oldexponent").Value
                    };

                    // Add to cache for 20min
                    memoryCache.Add("WopiProof", wopiProof, DateTimeOffset.Now.AddMinutes(20));
                }
            }

        }



        /// <summary>
        /// Forms the correct action url for the file and host
        /// </summary>
        public static string GetActionUrl(WopiAction action, string fileId, string authority)
        {
            // Initialize the urlsrc
            var urlsrc = action.urlsrc;

            // Look through the action placeholders
            var phCnt = 0;
            foreach (var p in WopiUrlPlaceholders.Placeholders)
            {
                if (urlsrc.Contains(p))
                {
                    // Replace the placeholder value accordingly
                    var ph = WopiUrlPlaceholders.GetPlaceholderValue(p);
                    if (!String.IsNullOrEmpty(ph))
                    {
                        urlsrc = urlsrc.Replace(p, ph + "&");
                        phCnt++;
                    }
                    else
                        urlsrc = urlsrc.Replace(p, ph);
                }
            }

            // Add the WOPISrc to the end of the request
            urlsrc += ((phCnt > 0) ? "" : "?") + String.Format("WOPISrc=https://{0}/wopi/files/{1}", authority, fileId);
            return urlsrc;
        }


        internal async static Task<WopiProof> getWopiProof()
        {
            var wopiProof = new WopiProof();
            // Check cache for this data
            MemoryCache memoryCache = MemoryCache.Default;
            if (!memoryCache.Contains("WopiProof"))
                await WopiDiscovery.Refresh();
            if (memoryCache.Contains("WopiProof"))
               wopiProof = (WopiProof)memoryCache["WopiProof"];
            return wopiProof;
        }
    }
}