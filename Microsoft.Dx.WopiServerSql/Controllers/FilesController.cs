using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Dx.Wopi.Controllers;
using Microsoft.Dx.Wopi.Models;
using System.Net.Http;
using Microsoft.Dx.WopiServerSql.Utils;
using Microsoft.Dx.WopiServerSql.Security;
using Microsoft.Dx.Wopi;
using Microsoft.Dx.WopiServerSql.Models;
using Microsoft.Dx.WopiServerSql.Repository;
using System.Net;
using System.Net.Mail;

namespace Microsoft.Dx.WopiServerSql.Controllers
{
    public class MyFilesController : WopiFilesController
    {
        public override Task<bool> Authorize(WopiRequest wopiRequest)
        {
            try
            {
                if (string.IsNullOrEmpty(wopiRequest.AccessToken))
                    return Task.FromResult<bool>(false);

                // Validate the access token contains authenticated user
                // We're only doing authentication here and deferring authorization to the other WOPI operations
                // to avoid multiple DB queries
                var userId = WopiSecurity.GetIdentityNameFromToken(wopiRequest.AccessToken);
                return Task.FromResult<bool>(userId != null);
            }
            catch (Exception)
            {
                // Any exception will return false, but should probably return an alternate status codes
                return Task.FromResult<bool>(false);
            }
        }

        public override async Task<WopiResponse> CheckFileInfo(CheckFileInfoRequest checkFileInfoRequest)
        {
            var userId = WopiSecurity.GetIdentityNameFromToken(checkFileInfoRequest.AccessToken);

            // For this demo server, determine tenant by host part of email address
            var tenant = new MailAddress(userId).Host.Replace(".", "-");

            // Lookup the file in the database using special repository method which grants access limited access to users in same tenant (same email domain)
            var wopiFileRepository = new WopiFileRepository();
            var response = await wopiFileRepository.GetFileInfoByTenantUser(checkFileInfoRequest.ResourceId, userId, tenant);

            // Check for null file
            if (response.Item1 == HttpStatusCode.NotFound)
                return checkFileInfoRequest.ResponseNotFound();

            else if (response.Item1 == HttpStatusCode.OK)
            {
                var wopiFile = response.Item2;
                // Get discovery information
                var actions = await WopiDiscovery.GetActions();
                string hostViewUrl = null, hostEditUrl = null;

                var closeUrl = String.Format("https://{0}", checkFileInfoRequest.RequestUri.Authority);

                var view = actions.FirstOrDefault(i => i.ext == wopiFile.FileExtension && i.name == "view");
                if (view != null)
                    hostViewUrl = WopiDiscovery.GetActionUrl(view, wopiFile.FileId.ToString(), checkFileInfoRequest.RequestUri.Authority);

                var edit = actions.FirstOrDefault(i => i.ext == wopiFile.FileExtension && i.name == "edit");
                if (edit != null)
                    hostEditUrl = WopiDiscovery.GetActionUrl(edit, wopiFile.FileId.ToString(), checkFileInfoRequest.RequestUri.Authority);

                // Write the response and return a success 200
                var wopiResponse = checkFileInfoRequest.ResponseOK(wopiFile.FileName, wopiFile.OwnerId, wopiFile.Size, userId, wopiFile.Version.ToString());
                // Add optional items
                wopiResponse.CloseUrl = new Uri(closeUrl);
                if (hostViewUrl != null)
                    wopiResponse.HostViewUrl = new Uri(hostViewUrl);
                if (hostEditUrl != null)
                    wopiResponse.HostEditUrl = new Uri(hostEditUrl);

                wopiResponse.UserInfo = wopiFile.FilePermissions.First().UserInfo;

                return wopiResponse;
            }
            else
                return checkFileInfoRequest.ResponseServerError(string.Format("Unknown response from WopiFileRepository.GetFileInfoByTenantUser: {0}", response.Item1));
        }

        public override async Task<WopiResponse> GetFile(GetFileRequest getFileRequest)
        {
            var userId = WopiSecurity.GetIdentityNameFromToken(getFileRequest.AccessToken);

            // Lookup the file in the database
            var wopiFileRepository = new WopiFileRepository();
            var response = await wopiFileRepository.GetFileContent(getFileRequest.ResourceId, userId);

            // Check for null file
            if (response.Item1 == HttpStatusCode.NotFound)
                return getFileRequest.ResponseNotFound();
            else
                // Write the response and return success 200
                return getFileRequest.ResponseOK(new StreamContent(response.Item2), response.Item3);
        }

        public override async Task<WopiResponse> Lock(LockRequest lockRequest)
        {
            var userId = WopiSecurity.GetIdentityNameFromToken(lockRequest.AccessToken);

            var wopiFileRepository = new WopiFileRepository();
            var response = await wopiFileRepository.LockFile(lockRequest.ResourceId, userId, lockRequest.Lock, null);

            if (response.Item1 == HttpStatusCode.BadRequest)
                return lockRequest.ResponseBadRequest();
            // Check for file not found or no permissions
            else if (response.Item1 == HttpStatusCode.NotFound)
                return lockRequest.ResponseNotFound();
            // Ensure the file isn't already locked
            else if (response.Item1 == HttpStatusCode.Conflict)
                return lockRequest.ResponseLockConflict(response.Item2);
            // File successfully locked
            else if (response.Item1 == HttpStatusCode.OK)
                return lockRequest.ResponseOK(response.Item3);
            else
                return lockRequest.ResponseServerError(string.Format("Unknown HTTPStatusCode from WopiFileRepository.LockFile: {0}", response.Item1));
        }

        public override async Task<WopiResponse> Unlock(UnlockRequest unlockRequest)
        {
            var userId = WopiSecurity.GetIdentityNameFromToken(unlockRequest.AccessToken);
            var wopiFileRepository = new WopiFileRepository();
            var response = await wopiFileRepository.UnlockFile(unlockRequest.ResourceId, userId, unlockRequest.Lock);

            if (response.Item1 == HttpStatusCode.BadRequest)
                return unlockRequest.ResponseBadRequest();
            // Check for file not found or no permissions
            else if (response.Item1 == HttpStatusCode.NotFound)
                return unlockRequest.ResponseNotFound();
            // Ensure the file isn't already locked
            else if (response.Item1 == HttpStatusCode.Conflict)
                return unlockRequest.ResponseLockConflict(response.Item2);
            // File successfully unlocked
            else if (response.Item1 == HttpStatusCode.OK)
                return unlockRequest.ResponseOK(response.Item3);
            else
                return unlockRequest.ResponseServerError(string.Format("Unknown HTTPStatusCode from WopiFileRepository.UnlockFile: {0}", response.Item1));
        }

        public override async Task<WopiResponse> UnlockAndRelock(UnlockAndRelockRequest unlockAndRelockRequest)
        {
            var userId = WopiSecurity.GetIdentityNameFromToken(unlockAndRelockRequest.AccessToken);

            var wopiFileRepository = new WopiFileRepository();
            var response = await wopiFileRepository.LockFile(unlockAndRelockRequest.ResourceId, userId, unlockAndRelockRequest.Lock, unlockAndRelockRequest.OldLock);

            if (response.Item1 == HttpStatusCode.BadRequest)
                return unlockAndRelockRequest.ResponseBadRequest();
            // Check for file not found or no permissions
            else if (response.Item1 == HttpStatusCode.NotFound)
                return unlockAndRelockRequest.ResponseNotFound();
            // Ensure the file isn't already locked
            else if (response.Item1 == HttpStatusCode.Conflict)
                return unlockAndRelockRequest.ResponseLockConflict(response.Item2);
            // File successfully locked
            else if (response.Item1 == HttpStatusCode.OK)
                return unlockAndRelockRequest.ResponseOK();
            else
                return unlockAndRelockRequest.ResponseServerError(string.Format("Unknown HTTPStatusCode from WopiFileRepository.LockFile: {0}", response.Item1));
        }


        public override async Task<WopiResponse> RefreshLock(RefreshLockRequest refreshLockRequest)
        {
            var userId = WopiSecurity.GetIdentityNameFromToken(refreshLockRequest.AccessToken);

            var wopiFileRepository = new WopiFileRepository();
            var response = await wopiFileRepository.LockFile(refreshLockRequest.ResourceId, userId, refreshLockRequest.Lock, null);

            if (response.Item1 == HttpStatusCode.BadRequest)
                return refreshLockRequest.ResponseBadRequest();
            // Check for file not found or no permissions
            else if (response.Item1 == HttpStatusCode.NotFound)
                return refreshLockRequest.ResponseNotFound();
            // Ensure the file isn't already locked
            else if (response.Item1 == HttpStatusCode.Conflict)
                return refreshLockRequest.ResponseLockConflict(response.Item2);
            // File successfully locked
            else if (response.Item1 == HttpStatusCode.OK)
                return refreshLockRequest.ResponseOK();
            else
                return refreshLockRequest.ResponseServerError(string.Format("Unknown HTTPStatusCode from WopiFileRepository.LockFile: {0}", response.Item1));
        }

        public override async Task<WopiResponse> GetLock(GetLockRequest getLockRequest)
        {
            var userId = WopiSecurity.GetIdentityNameFromToken(getLockRequest.AccessToken);
            var wopiFileRepository = new WopiFileRepository();
            var response = await wopiFileRepository.GetLockStatus(getLockRequest.ResourceId, userId);

            // Check for file not found or no permissions
            if (response.Item1 == HttpStatusCode.NotFound)
                return getLockRequest.ResponseNotFound();
            // Ensure the file isn't already locked
            else if (response.Item1 == HttpStatusCode.Conflict)
                return getLockRequest.ResponseLockConflict(response.Item2);
            // File successfully locked
            else if (response.Item1 == HttpStatusCode.OK)
            {
                if (response.Item2 != null)
                    return getLockRequest.ResponseFileLocked(response.Item2);
                else
                    return getLockRequest.ResponseFileNotLocked();
            }
            else
                return getLockRequest.ResponseServerError(string.Format("Unknown HTTPStatusCode from WopiFileRepository.GetLockStatus: {0}", response.Item1));

        }

        public override async Task<WopiResponse> PutRelativeFileSpecific(PutRelativeFileSpecificRequest putRelativeFileSpecificRequest)
        {
            var userId = WopiSecurity.GetIdentityNameFromToken(putRelativeFileSpecificRequest.AccessToken);

            var wopiFileRepository = new WopiFileRepository();
            var response = await wopiFileRepository.CreateCopy(putRelativeFileSpecificRequest.ResourceId, userId, putRelativeFileSpecificRequest.RelativeTarget, putRelativeFileSpecificRequest.OverwriteRelativeTarget);
            if (response.Item1 == HttpStatusCode.NotFound)
                return putRelativeFileSpecificRequest.ResponseNotFound();
            else if (response.Item1 == HttpStatusCode.BadRequest)
                return putRelativeFileSpecificRequest.ResponseBadRequest();
            else if (response.Item1 == HttpStatusCode.Conflict)
                return putRelativeFileSpecificRequest.ResponseLockConflict(response.Item3);
            else if (response.Item1 == HttpStatusCode.OK)
            {
                // Get access token for the new file
                WopiSecurity security = new WopiSecurity();
                var token = security.GenerateToken(response.Item2.OwnerId);
                var tokenStr = security.WriteToken(token);

                var url = new Uri(string.Format("https://{0}/wopi/files/{1}?access_token={2}",
                    putRelativeFileSpecificRequest.RequestUri.Authority, response.Item2.FileId, tokenStr));

                Uri hostViewUrl = null;
                Uri hostEditUrl = null;
                var actions = await WopiDiscovery.GetActions();
                var view = actions.FirstOrDefault(i => i.ext == response.Item2.FileExtension && i.name == "view");
                if (view != null)
                    hostViewUrl = new Uri(WopiDiscovery.GetActionUrl(view, response.Item2.FileId, putRelativeFileSpecificRequest.RequestUri.Authority));
                var edit = actions.FirstOrDefault(i => i.ext == response.Item2.FileExtension && i.name == "edit");
                if (edit != null)
                    hostEditUrl = new Uri(WopiDiscovery.GetActionUrl(edit, response.Item2.FileId, putRelativeFileSpecificRequest.RequestUri.Authority));

                return putRelativeFileSpecificRequest.ResponseOK(response.Item2.FileName, url, hostViewUrl, hostEditUrl);
            }
            else
                return putRelativeFileSpecificRequest.ResponseServerError(string.Format("Unknown HTTPStatusCode from WopiFileRepository.CreateCopy: {0}", response.Item1));

        }

        public override async Task<WopiResponse> PutRelativeFileSuggested(PutRelativeFileSuggestedRequest putRelativeFileSuggestedRequest)
        {
            var userId = WopiSecurity.GetIdentityNameFromToken(putRelativeFileSuggestedRequest.AccessToken);

            var wopiFileRepository = new WopiFileRepository();
            var response = await wopiFileRepository.CreateCopySuggested(putRelativeFileSuggestedRequest.ResourceId, userId, putRelativeFileSuggestedRequest.SuggestedTarget);
            if (response.Item1 == HttpStatusCode.NotFound)
                return putRelativeFileSuggestedRequest.ResponseNotFound();
            else if (response.Item1 == HttpStatusCode.OK)
            {
                // Get access token for the new file
                WopiSecurity security = new WopiSecurity();
                var token = security.GenerateToken(response.Item2.OwnerId);
                var tokenStr = security.WriteToken(token);

                //var name = newFile.BaseFileName;

                var url = new Uri(string.Format("https://{0}/wopi/files/{1}?access_token={2}",
                    putRelativeFileSuggestedRequest.RequestUri.Authority, response.Item2.FileId, tokenStr));

                // Add the optional properties to response if applicable (HostViewUrl, HostEditUrl)
                Uri hostViewUrl = null;
                Uri hostEditUrl = null;
                var actions = await WopiDiscovery.GetActions();
                var view = actions.FirstOrDefault(i => i.ext == response.Item2.FileExtension && i.name == "view");
                if (view != null)
                {
                    hostViewUrl = new Uri(WopiDiscovery.GetActionUrl(view, response.Item2.FileId, putRelativeFileSuggestedRequest.RequestUri.Authority));
                }
                var edit = actions.FirstOrDefault(i => i.ext == response.Item2.FileExtension && i.name == "edit");
                if (edit != null)
                {
                    hostEditUrl = new Uri(WopiDiscovery.GetActionUrl(edit, response.Item2.FileId, putRelativeFileSuggestedRequest.RequestUri.Authority));
                }
                // Write the response and return a success 200
                return putRelativeFileSuggestedRequest.ResponseOK(response.Item2.FileName, url, hostViewUrl, hostEditUrl);
            }
            else
                return putRelativeFileSuggestedRequest.ResponseServerError(string.Format("Unknown HTTPStatusCode from WopiFileRepository.CreateCopySuggested: {0}", response.Item1));
        }

        public override async Task<WopiResponse> RenameFile(RenameFileRequest renameFileRequest)
        {
            var userId = WopiSecurity.GetIdentityNameFromToken(renameFileRequest.AccessToken);

            var wopiFileRepository = new WopiFileRepository();
            var response = await wopiFileRepository.RenameFile(renameFileRequest.ResourceId, userId, renameFileRequest.Lock, renameFileRequest.RequestedName);
            if (response.Item1 == HttpStatusCode.NotFound)
                return renameFileRequest.ResponseNotFound();
            else if (response.Item1 == HttpStatusCode.Conflict)
                return renameFileRequest.ResponseLockConflict(response.Item2);
            else if (response.Item1 == HttpStatusCode.BadRequest)
                return renameFileRequest.ResponseBadRequest(response.Item2);
            else if (response.Item1 == HttpStatusCode.OK)
                return renameFileRequest.ResponseOK(response.Item2);
            else
                return renameFileRequest.ResponseServerError(string.Format("Unknown HTTPStatusCode from WopiFileRepository.RenameFile: {0}", response.Item1));

        }

        public override async Task<WopiResponse> PutUserInfo(PutUserInfoRequest putUserInfoRequest)
        {
            var userId = WopiSecurity.GetIdentityNameFromToken(putUserInfoRequest.AccessToken);
            var wopiFileRespository = new WopiFileRepository();
            var response = await wopiFileRespository.SaveWopiUserInfo(putUserInfoRequest.ResourceId, userId, putUserInfoRequest.UserInfo);
            if (response == HttpStatusCode.NotFound)
                return putUserInfoRequest.ResponseNotFound();
            else if (response == HttpStatusCode.OK)
                return putUserInfoRequest.ResponseOK();
            else
                return putUserInfoRequest.ResponseServerError(string.Format("Unknown HTTPStatusCode from WopiFileRepository.SaveWopiUserInfo: {0}", response));
        }

        public override async Task<WopiResponse> PutFile(PutFileRequest putFileRequest)
        {
            var userId = WopiSecurity.GetIdentityNameFromToken(putFileRequest.AccessToken);
            var wopiFileRespository = new WopiFileRepository();
            var response = await wopiFileRespository.UpdateFileContent(putFileRequest.ResourceId, userId, putFileRequest.Lock, await putFileRequest.Content.ReadAsStreamAsync());
            if (response.Item1 == HttpStatusCode.NotFound)
                return putFileRequest.ResponseNotFound();
            else if (response.Item1 == HttpStatusCode.Conflict)
                return putFileRequest.ResponseLockConflict(response.Item2);
            else if (response.Item1 == HttpStatusCode.OK)
                return putFileRequest.ResponseOK(response.Item3);
            else
                return putFileRequest.ResponseServerError(string.Format("Unknown HTTPStatusCode from WopiFileRepository.UpdateFileContent: {0}", response.Item1));
        }

        public override async Task<WopiResponse> DeleteFile(DeleteFileRequest deleteFileRequest)
        {
            var userId = WopiSecurity.GetIdentityNameFromToken(deleteFileRequest.AccessToken);
            var wopiFileRespository = new WopiFileRepository();
            var response = await wopiFileRespository.DeleteFile(deleteFileRequest.ResourceId, userId);
            if (response == HttpStatusCode.NotFound)
                return deleteFileRequest.ResponseNotFound();
            else if (response == HttpStatusCode.OK)
                return deleteFileRequest.ResponseOK();
            else
                return deleteFileRequest.ResponseServerError(string.Format("Unknown HTTPStatusCode from WopiFileRepository.UpdateFileContent: {0}", response));
        }
    }
}