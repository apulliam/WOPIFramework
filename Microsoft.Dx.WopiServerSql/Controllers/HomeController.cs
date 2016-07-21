using Microsoft.Dx.Wopi;
using Microsoft.Dx.Wopi.Models;
using Microsoft.Dx.WopiServerSql.Models;
using Microsoft.Dx.WopiServerSql.Repository;
using Microsoft.Dx.WopiServerSql.Security;
using Microsoft.Dx.WopiServerSql.Utils;
using Microsoft.Owin.Security.OAuth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Runtime.Caching;
using System.Security.Cryptography;
using System.Data.Entity;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace Microsoft.Dx.WopiServerSql.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        /// <summary>
        /// Index view displays all files for the signed in user
        /// </summary>
        [Authorize]
        public async Task<ActionResult> Index()
        {
            var userId = User.Identity.Name;
            var tenant = new MailAddress(userId).Host.Replace(".", "-");

            var wopiFileRepository = new WopiFileRepository();
            var wopiFiles = await wopiFileRepository.GetFilesByTenant(tenant);

            var wopiFileModels = new List<WopiFileModel>();

            foreach (var wopiFile in wopiFiles)
                wopiFileModels.Add(await wopiFile.ToWopiFileModel());
        
            
            // Return the view with the files
            return View(wopiFileModels);
        }

        /// <summary>
        /// Detail view hosts the WOPI host frame and loads the appropriate action view from Office Online
        /// </summary>
        [Authorize]
        [Route("Home/Detail/{id}")]
        public async Task<ActionResult> Detail(string id)
        {
            var userId = User.Identity.Name;
            var tenant = new MailAddress(userId).Host.Replace(".", "-");

            // Make sure an action was passed in
            if (String.IsNullOrEmpty(Request["action"]))
                return RedirectToAction("Error", "Home", new { error = "No action provided" });
            
            var wopiFileRepository = new WopiFileRepository();
            var result = await wopiFileRepository.GetFileInfoByTenantUser(id, userId, tenant);

            // Check for null file
            if (result.Item1 == HttpStatusCode.NotFound)
                return RedirectToAction("Error", "Home", new { error = "Files does not exist" });
            else if (result.Item1 == HttpStatusCode.Unauthorized)
                return RedirectToAction("Error", "Home", new { error = "Not authorized to access file" });
            else if (result.Item1 == HttpStatusCode.OK)
            {
                var wopiFile = result.Item2;
                // Use discovery to determine endpoint to leverage
                List<WopiAction> discoData = await WopiDiscovery.GetActions();
                var fileExt = wopiFile.FileName.Substring(wopiFile.FileName.LastIndexOf('.') + 1).ToLower();
                var action = discoData.FirstOrDefault(i => i.name == Request["action"] && i.ext == fileExt);

                // Make sure the action isn't null
                if (action != null)
                {
                    string urlsrc = WopiDiscovery.GetActionUrl(action, wopiFile.FileId.ToString(), Request.Url.Authority);

                    // Generate JWT token for the user/document
                    WopiSecurity wopiSecurity = new WopiSecurity();
                    var token = wopiSecurity.GenerateToken(User.Identity.Name.ToLower());
                    ViewData["access_token"] = wopiSecurity.WriteToken(token);
                    ViewData["access_token_ttl"] = token.ValidTo.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
                    ViewData["wopi_urlsrc"] = urlsrc;
                    return View();
                }
                else
                {
                    // This will only hit if the extension isn't supported by WOPI
                    return RedirectToAction("Error", "Home", new { error = "File is not a supported WOPI extension" });
                }
            }
            else
                return RedirectToAction("Error", "Home", new { error = "Internal server error" });
        }

        /// <summary>
        /// Adds the submitted files for Azure Blob Storage and metadata into DocumentDB
        /// </summary>
        [HttpPost]
        [Authorize]
        public async Task<ActionResult> Add()
        {
            // This method is called by JavaScript, so use try/catch block to make sure an error gets returned
            try
            {
                var userId = User.Identity.Name;
                var tenant = new MailAddress(userId).Host.Replace(".", "-");

                var fileName = HttpUtility.UrlDecode(Request["HTTP_X_FILE_NAME"]);
                var size = Convert.ToInt32(Request["HTTP_X_FILE_SIZE"]);


                var wopiFileRepository = new WopiFileRepository();
                var wopiFile = await wopiFileRepository.AddFile(userId, tenant, Request.InputStream, fileName);

                if (wopiFile != null)
                {
                    // Return json representation of information
                    return Json(new { success = true, file = await wopiFile.ToWopiFileModel() });
                }
                else
                    // Something failed...return false
                    return Json(new { success = false });
            }
            catch (Exception)
            {
                // Something failed...return false
                return Json(new { success = false });
            }
        }

        /// <summary>
        /// Deletes the file from Azure Blob Storage and metadata in SQL Server
        /// </summary>
        [HttpDelete]
        [Authorize]
        [Route("Home/Delete/{id}")]
        public async Task<ActionResult> Delete(string id)
        {
            // This method is called by JavaScript, so use try/catch block to make sure an error gets returned
            try
            {
                var userId = User.Identity.Name;
                var wopiFileRepository = new WopiFileRepository();
                var statusCode = await wopiFileRepository.DeleteFile(id, userId);
                if (statusCode == HttpStatusCode.OK)
                    //return json representation of information
                    return Json(new { success = true, id = id.ToString() });
                else
                    // Something failed...return false
                    return Json(new { success = false, id = id.ToString() });
            }
            catch (Exception)
            {
                // Something failed...return false
                return Json(new { success = false, id = id.ToString() });
            }
        }

        /// <summary>
        /// Error view displays error messages passed from other controllers
        /// </summary>
        public ActionResult Error(string error)
        {
            ViewData["Error"] = error;
            return View();
        }

    }
}