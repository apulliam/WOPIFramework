using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Dx.Wopi.Controllers;
using Microsoft.Dx.Wopi.Models;
using System.Net.Http;
using Microsoft.Dx.WopiServerDocumentDb.Utils;
using Microsoft.Dx.WopiServerDocumentDb.Security;
using Microsoft.Dx.Wopi;
using Microsoft.Dx.WopiServerDocumentDb.Models;

namespace Microsoft.Dx.WopiServerDocumentDb.Controllers
{
    public class MyFilesController : WopiFilesController
    {
        public override Task<bool> Authorize(WopiRequest wopiRequest)
        {
            try
            {
                if (string.IsNullOrEmpty(wopiRequest.AccessToken))
                    return Task.FromResult<bool>(false);

                // Get the requested file from Document DB
                var itemId = new Guid(wopiRequest.ResourceId);
                var wopiFile = DocumentDBRepository<DetailedFileModel>.GetItem("Files", file => file.id == itemId);

                // Check for missing file
                if (wopiFile == null)
                    return Task.FromResult<bool>(false);

                // Validate the access token
                return Task.FromResult<bool>(WopiSecurity.ValidateToken(wopiRequest.AccessToken, wopiFile.Container, wopiFile.id.ToString()));
            }
            catch (Exception)
            {
                // Any exception will return false, but should probably return an alternate status codes
                return Task.FromResult<bool>(false);
            }
        }


        public override async Task<WopiResponse> CheckFileInfo(CheckFileInfoRequest checkFileInfoRequest)
        {
            // Lookup the file in the database
            var itemId = new Guid(checkFileInfoRequest.ResourceId);
            var wopiFile = DocumentDBRepository<DetailedFileModel>.GetItem("Files", file => file.id == itemId);

            // Check for null file
            if (wopiFile != null)
            {
                // Get discovery information
                var fileExt = wopiFile.BaseFileName.Substring(wopiFile.BaseFileName.LastIndexOf('.') + 1).ToLower();
                var actions = await WopiDiscovery.GetActions();

                // Augments the file with additional properties CloseUrl, HostViewUrl, HostEditUrl
                wopiFile.CloseUrl = String.Format("https://{0}", checkFileInfoRequest.RequestUri.Authority);
                var view = actions.FirstOrDefault(i => i.ext == fileExt && i.name == "view");
                if (view != null)
                    wopiFile.HostViewUrl = WopiDiscovery.GetActionUrl(view, wopiFile.id.ToString(), checkFileInfoRequest.RequestUri.Authority);
                var edit = actions.FirstOrDefault(i => i.ext == fileExt && i.name == "edit");
                if (edit != null)
                    wopiFile.HostEditUrl = WopiDiscovery.GetActionUrl(edit, wopiFile.id.ToString(), checkFileInfoRequest.RequestUri.Authority);

                // Get the user from the token (token is already validated)
                wopiFile.UserId = WopiSecurity.GetUserFromToken(checkFileInfoRequest.AccessToken);

                // Write the response and return a success 200
                var wopiResponse = checkFileInfoRequest.ResponseOK(wopiFile.BaseFileName, wopiFile.OwnerId, wopiFile.Size, wopiFile.UserId, wopiFile.Version.ToString());
                // Add optional items
                wopiResponse.CloseUrl = new Uri(wopiFile.CloseUrl);
                if (wopiFile.HostViewUrl != null)
                    wopiResponse.HostViewUrl = new Uri(wopiFile.HostViewUrl);
                if (wopiFile.HostEditUrl != null)
                    wopiResponse.HostEditUrl = new Uri(wopiFile.HostEditUrl);
               
                return wopiResponse;
            }
            else
            {
                return checkFileInfoRequest.ResponseNotFound();
            }
        }

        public override async Task<WopiResponse> GetFile(GetFileRequest getFileRequest)
        {
        
            // Lookup the file in the database
            var itemId = new Guid(getFileRequest.ResourceId);
            var wopiFile = DocumentDBRepository<DetailedFileModel>.GetItem("Files", file => file.id == itemId);

            // Check for null file
            if (wopiFile != null)
            {
                // Get discovery information
                var fileExt = wopiFile.BaseFileName.Substring(wopiFile.BaseFileName.LastIndexOf('.') + 1).ToLower();
                var actions = await WopiDiscovery.GetActions();

                // Augments the file with additional properties CloseUrl, HostViewUrl, HostEditUrl
                wopiFile.CloseUrl = String.Format("https://{0}", getFileRequest.RequestUri.Authority);
                var view = actions.FirstOrDefault(i => i.ext == fileExt && i.name == "view");
                if (view != null)
                    wopiFile.HostViewUrl = WopiDiscovery.GetActionUrl(view, wopiFile.id.ToString(), getFileRequest.RequestUri.Authority);
                var edit = actions.FirstOrDefault(i => i.ext == fileExt && i.name == "edit");
                if (edit != null)
                    wopiFile.HostEditUrl = WopiDiscovery.GetActionUrl(edit, wopiFile.id.ToString(), getFileRequest.RequestUri.Authority);

                // Get the user from the token (token is already validated)
                wopiFile.UserId = WopiSecurity.GetUserFromToken(getFileRequest.AccessToken);

                // Call the appropriate handler for the WOPI request we received
                // Get the file from blob storage
                var bytes = await AzureStorageUtil.GetFile(wopiFile.id.ToString(), wopiFile.Container);

                // Write the response and return success 200
                return getFileRequest.ResponseOK(new ByteArrayContent(bytes));
            }
            else
            {
                return getFileRequest.ResponseNotFound();
            }
        }

        public override async Task<WopiResponse> Lock(LockRequest lockRequest)
        {
            WopiResponse wopiResponse = null;
            var file = DocumentDBRepository<DetailedFileModel>.GetItem("Files", i => i.id.ToString() == lockRequest.ResourceId);

            // Check for null file
            if (file != null)
            {
                // Ensure the file isn't already locked or expired
                if (String.IsNullOrEmpty(file.LockValue) ||
                    (file.LockExpires != null &&
                    file.LockExpires < DateTime.Now))
                {
                    // Update the file with a LockValue and LockExpiration
                    file.LockValue = lockRequest.Lock;
                    file.LockExpires = DateTime.Now.AddMinutes(30);
                    await DocumentDBRepository<FileModel>.UpdateItemAsync("Files", file.id.ToString(), (FileModel)file);

                    // Return success 200
                    wopiResponse = lockRequest.ResponseOK(file.Version.ToString());
                }
                else if (file.LockValue == lockRequest.Lock)
                {
                    // File lock matches existing lock, so refresh lock by extending expiration
                    file.LockExpires = DateTime.Now.AddMinutes(30);
                    await DocumentDBRepository<FileModel>.UpdateItemAsync("Files", file.id.ToString(), (FileModel)file);

                    // Return success 200
                    wopiResponse = lockRequest.ResponseOK(file.Version.ToString());
                }
                else
                {
                    // The file is locked by someone else...return mismatch
                    wopiResponse = lockRequest.ResponseLockConflict(file.LockValue, String.Format("File already locked by {0}", file.LockValue));
                }
            }
            else
            {
                wopiResponse = lockRequest.ResponseNotFound();
            }
            return wopiResponse;
        }

        public override async Task<WopiResponse> Unlock(UnlockRequest unlockRequest)
        {
            WopiResponse wopiResponse = null;
            var file = DocumentDBRepository<DetailedFileModel>.GetItem("Files", i => i.id.ToString() == unlockRequest.ResourceId);

            // Check for null file
            if (file != null)
            {
                // Ensure the file has a valid lock
                if (String.IsNullOrEmpty(file.LockValue))
                {
                    // File isn't locked...pass empty Lock in mismatch response
                    return unlockRequest.ResponseLockConflict("File isn't locked");
                }
                else if (file.LockExpires != null && file.LockExpires < DateTime.Now)
                {
                    // File lock expired, so clear it out
                    file.LockValue = null;
                    file.LockExpires = null;
                    await DocumentDBRepository<FileModel>.UpdateItemAsync("Files", file.id.ToString(), (FileModel)file);

                    // File isn't locked...pass empty Lock in mismatch response
                    wopiResponse = unlockRequest.ResponseLockConflict(String.Empty, "File isn't locked");
                }
                else if (unlockRequest.Lock != file.LockValue)
                {
                    // File lock mismatch...pass Lock in mismatch response
                    wopiResponse = unlockRequest.ResponseLockConflict(file.LockValue, "Lock mismatch");
                }
                else
                {
                    // Unlock the file
                    file.LockValue = null;
                    file.LockExpires = null;
                    await DocumentDBRepository<FileModel>.UpdateItemAsync("Files", file.id.ToString(), (FileModel)file);

                    // Return success 200
                    wopiResponse = unlockRequest.ResponseOK();
                }
            }
            else
            {
                wopiResponse = unlockRequest.ResponseNotFound();
            }
            return wopiResponse;
        
        }

        public override async Task<WopiResponse> UnlockAndRelock(UnlockAndRelockRequest unlockAndRelockRequest)
        {
            WopiResponse wopiResponse = null;
            var file = DocumentDBRepository<DetailedFileModel>.GetItem("Files", i => i.id.ToString() == unlockAndRelockRequest.ResourceId);

            // Check for null file
            if (file != null)
            {
                // Ensure the file has a valid lock
                if (String.IsNullOrEmpty(file.LockValue))
                {
                    // File isn't locked...pass empty Lock in mismatch response
                    wopiResponse = unlockAndRelockRequest.ResponseLockConflict(String.Empty, "File isn't locked");
                }
                else if (file.LockExpires != null && file.LockExpires < DateTime.Now)
                {
                    // File lock expired, so clear it out
                    file.LockValue = null;
                    file.LockExpires = null;
                    await DocumentDBRepository<FileModel>.UpdateItemAsync("Files", file.id.ToString(), (FileModel)file);

                    // File isn't locked...pass empty Lock in mismatch response
                    wopiResponse = unlockAndRelockRequest.ResponseLockConflict(String.Empty, "File isn't locked");
                }
                else if (unlockAndRelockRequest.OldLock != file.LockValue)
                {
                    // File lock mismatch...pass Lock in mismatch response
                    wopiResponse = unlockAndRelockRequest.ResponseLockConflict(file.LockValue, "Lock mismatch");
                }
                else
                {
                    // Update the file with a LockValue and LockExpiration
                    file.LockValue = unlockAndRelockRequest.Lock;
                    file.LockExpires = DateTime.Now.AddMinutes(30);
                    await DocumentDBRepository<FileModel>.UpdateItemAsync("Files", file.id.ToString(), (FileModel)file);

                    // Return success 200
                    wopiResponse = unlockAndRelockRequest.ResponseOK();
                }
            }
            else
            {
                wopiResponse = wopiResponse = unlockAndRelockRequest.ResponseNotFound();
            }
            return wopiResponse;
        
        }

        public override async Task<WopiResponse> RefreshLock(RefreshLockRequest refreshLockRequest)
        {
            WopiResponse wopiResponse = null;
            var file = DocumentDBRepository<DetailedFileModel>.GetItem("Files", i => i.id.ToString() == refreshLockRequest.ResourceId);

            // Check for null file
            if (file != null)
            {
                // Ensure the file has a valid lock
                if (String.IsNullOrEmpty(file.LockValue))
                {
                    // File isn't locked...pass empty Lock in mismatch response
                    wopiResponse = refreshLockRequest.ResponseLockConflict(String.Empty, "File isn't locked");
                }
                else if (file.LockExpires != null && file.LockExpires < DateTime.Now)
                {
                    // File lock expired, so clear it out
                    file.LockValue = null;
                    file.LockExpires = null;
                    await DocumentDBRepository<FileModel>.UpdateItemAsync("Files", file.id.ToString(), (FileModel)file);

                    // File isn't locked...pass empty Lock in mismatch response
                    wopiResponse = refreshLockRequest.ResponseLockConflict(String.Empty, "File isn't locked");
                }
                else if (refreshLockRequest.Lock != file.LockValue)
                {
                    // File lock mismatch...pass Lock in mismatch response
                    wopiResponse = refreshLockRequest.ResponseLockConflict(file.LockValue, "Lock mismatch");
                }
                else
                {
                    // Extend the expiration
                    file.LockExpires = DateTime.Now.AddMinutes(30);
                    await DocumentDBRepository<FileModel>.UpdateItemAsync("Files", file.id.ToString(), (FileModel)file);

                    // Return success 200
                    wopiResponse = refreshLockRequest.ResponseOK();
                }
            }
            else
            {
                wopiResponse = refreshLockRequest.ResponseNotFound();
            }
            return wopiResponse;
        }

        public override async Task<WopiResponse> GetLock(GetLockRequest getLockRequest)
        {
            WopiResponse wopiResponse = null;
            var file = DocumentDBRepository<DetailedFileModel>.GetItem("Files", i => i.id.ToString() == getLockRequest.ResourceId);

            // Check for null file
            if (file != null)
            {
                // Check for valid lock on file
                if (String.IsNullOrEmpty(file.LockValue))
                {
                    // File is not locked...return empty X-WOPI-Lock header
                    // Return success 200
                    wopiResponse = getLockRequest.ResponseFileNotLocked();
                }
                else if (file.LockExpires != null && file.LockExpires < DateTime.Now)
                {
                    // File lock expired, so clear it out
                    file.LockValue = null;
                    file.LockExpires = null;
                    await DocumentDBRepository<FileModel>.UpdateItemAsync("Files", file.id.ToString(), (FileModel)file);

                    // File is not locked...return empty X-WOPI-Lock header
                    // Return success 200
                    wopiResponse = getLockRequest.ResponseFileNotLocked();
                }
                else
                {
                    // Return success 200
                    wopiResponse = getLockRequest.ResponseFileLocked(file.LockValue);
                }
            }
            else
            {
                wopiResponse = getLockRequest.ResponseNotFound();
            }
           
            return wopiResponse;
        }

        public override async Task<WopiResponse> PutRelativeFileSpecific(PutRelativeFileSpecificRequest putRelativeFileSpecificRequest)
        {
            WopiResponse wopiResponse = null;
            var file = DocumentDBRepository<DetailedFileModel>.GetItem("Files", i => i.id.ToString() == putRelativeFileSpecificRequest.ResourceId);

            // Check for null file
            if (file != null)
            {
                var inputStream = await putRelativeFileSpecificRequest.Content.ReadAsStreamAsync();
                // Create the file entity
                FileModel newFile = new FileModel()
                {
                    id = Guid.NewGuid(),
                    OwnerId = file.OwnerId,
                    BaseFileName = putRelativeFileSpecificRequest.RelativeTarget,
                    Size = inputStream.Length,
                    Container = file.Container,
                    Version = 1
                   
                };

                // First stream the file into blob storage
                var bytes = new byte[inputStream.Length];
                await inputStream.ReadAsync(bytes, 0, (int)inputStream.Length);
                var id = await Utils.AzureStorageUtil.UploadFile(newFile.id.ToString(), newFile.Container, bytes);

                // Write the details into documentDB
                await DocumentDBRepository<FileModel>.CreateItemAsync("Files", (FileModel)newFile);

                // Get access token for the new file
                WopiSecurity security = new WopiSecurity();
                var token = security.GenerateToken(newFile.OwnerId, newFile.Container, newFile.id.ToString());
                var tokenStr = security.WriteToken(token);

                // Prepare the Json response
                var name = newFile.BaseFileName;
                var url = string.Format("https://{0}/wopi/files/{1}?access_token={2}",
                    putRelativeFileSpecificRequest.RequestUri.Authority, newFile.id.ToString(), tokenStr);

                // Add the optional properties to response if applicable (HostViewUrl, HostEditUrl)
                var fileExt = newFile.BaseFileName.Substring(newFile.BaseFileName.LastIndexOf('.') + 1).ToLower();

                string hostViewUrl = null;
                string hostEditUrl = null;
                var actions = await WopiDiscovery.GetActions();
                var view = actions.FirstOrDefault(i => i.ext == fileExt && i.name == "view");
                if (view != null)
                    hostViewUrl = WopiDiscovery.GetActionUrl(view, newFile.id.ToString(), putRelativeFileSpecificRequest.RequestUri.Authority);
                var edit = actions.FirstOrDefault(i => i.ext == fileExt && i.name == "edit");
                if (edit != null)
                    hostEditUrl = WopiDiscovery.GetActionUrl(edit, newFile.id.ToString(), putRelativeFileSpecificRequest.RequestUri.Authority);
                  
                // Write the response and return a success 200
                wopiResponse = putRelativeFileSpecificRequest.ResponseOK(name, new Uri(url), new Uri(hostViewUrl), new Uri(hostEditUrl));
                
            }
            else
            {
                wopiResponse = putRelativeFileSpecificRequest.ResponseNotFound();
            }
            return wopiResponse;
        }

        public override async Task<WopiResponse> PutRelativeFileSuggested(PutRelativeFileSuggestedRequest putRelativeFileSuggestedRequest)
        {
            WopiResponse wopiResponse = null;
            var file = DocumentDBRepository<DetailedFileModel>.GetItem("Files", i => i.id.ToString() == putRelativeFileSuggestedRequest.ResourceId);

            // Check for null file
            if (file != null)
            {
                var inputStream = await putRelativeFileSuggestedRequest.Content.ReadAsStreamAsync();

                // Suggested mode...might just be an extension
                var fileName = putRelativeFileSuggestedRequest.SuggestedTarget;
                if (fileName.IndexOf('.') == 0)
                    fileName = file.BaseFileName.Substring(0, file.BaseFileName.LastIndexOf('.')) + fileName;
                
                // Create the file entity
                FileModel newFile = new FileModel()
                {
                    id = Guid.NewGuid(),
                    OwnerId = file.OwnerId,
                    BaseFileName = fileName,
                    Size = inputStream.Length,
                    Container = file.Container,
                    Version = 1
                };

                // First stream the file into blob storage
                var bytes = new byte[inputStream.Length];
                await inputStream.ReadAsync(bytes, 0, (int)inputStream.Length);
                var id = await Utils.AzureStorageUtil.UploadFile(newFile.id.ToString(), newFile.Container, bytes);

                // Write the details into documentDB
                await DocumentDBRepository<FileModel>.CreateItemAsync("Files", (FileModel)newFile);

                // Get access token for the new file
                WopiSecurity security = new WopiSecurity();
                var token = security.GenerateToken(newFile.OwnerId, newFile.Container, newFile.id.ToString());
                var tokenStr = security.WriteToken(token);

                // Prepare the Json response
                var name = newFile.BaseFileName;

                var url = string.Format("https://{0}/wopi/files/{1}?access_token={2}",
                    putRelativeFileSuggestedRequest.RequestUri.Authority, newFile.id.ToString(), tokenStr);

                // Add the optional properties to response if applicable (HostViewUrl, HostEditUrl)
                string hostViewUrl = null;
                string hostEditUrl = null;
                var actions = await WopiDiscovery.GetActions();
                var fileExt = newFile.BaseFileName.Substring(newFile.BaseFileName.LastIndexOf('.') + 1).ToLower();
                var view = actions.FirstOrDefault(i => i.ext == fileExt && i.name == "view");
                if (view != null)
                    hostViewUrl = WopiDiscovery.GetActionUrl(view, newFile.id.ToString(), putRelativeFileSuggestedRequest.RequestUri.Authority);
                var edit = actions.FirstOrDefault(i => i.ext == fileExt && i.name == "edit");
                if (edit != null)
                    hostEditUrl = WopiDiscovery.GetActionUrl(edit, newFile.id.ToString(), putRelativeFileSuggestedRequest.RequestUri.Authority);
              
                // Write the response and return a success 200
                wopiResponse = putRelativeFileSuggestedRequest.ResponseOK(fileName, new Uri(url), new Uri(hostViewUrl), new Uri(hostEditUrl));
            }
            else
            {
                wopiResponse = putRelativeFileSuggestedRequest.ResponseNotFound();
            }
            return wopiResponse;
        }

        public override async Task<WopiResponse> RenameFile(RenameFileRequest renameFileRequest)
        {
            WopiResponse wopiResponse = null;
            var file = DocumentDBRepository<DetailedFileModel>.GetItem("Files", i => i.id.ToString() == renameFileRequest.ResourceId);

            // Check for null file
            if (file != null)
            {
                // Make sure the X-WOPI-RequestedName header is included
                if (renameFileRequest.RequestedName != null)
                {
                    // Ensure the file isn't locked
                    if (String.IsNullOrEmpty(file.LockValue) ||
                        (file.LockExpires != null &&
                        file.LockExpires < DateTime.Now))
                    {
                        // Update the file with a LockValue and LockExpiration
                        file.LockValue = renameFileRequest.Lock;
                        file.LockExpires = DateTime.Now.AddMinutes(30);
                        file.BaseFileName = renameFileRequest.RequestedName;
                        await DocumentDBRepository<FileModel>.UpdateItemAsync("Files", file.id.ToString(), (FileModel)file);

                        // Return success 200
                        wopiResponse = renameFileRequest.ResponseOK(renameFileRequest.RequestedName);
                    }
                    else if (file.LockValue == renameFileRequest.Lock)
                    {
                        // File lock matches existing lock, so we can change the name
                        file.LockExpires = DateTime.Now.AddMinutes(30);
                        file.BaseFileName = renameFileRequest.RequestedName;
                        await DocumentDBRepository<FileModel>.UpdateItemAsync("Files", file.id.ToString(), (FileModel)file);

                        // Return success 200
                        wopiResponse = renameFileRequest.ResponseOK(renameFileRequest.RequestedName);
                    }
                    else
                    {
                        // The file is locked by someone else...return mismatch
                        wopiResponse = renameFileRequest.ResponseLockConflict(file.LockValue, String.Format("File locked by {0}", file.LockValue));
                    }
                }
                else
                {
                    // X-WOPI-RequestedName header wasn't included
                    wopiResponse = renameFileRequest.ResponseBadRequest("X-WOPI-RequestedName header wasn't included in request");
                }
            }
            else
            {
                wopiResponse = renameFileRequest.ResponseNotFound();
            }
            return wopiResponse;
        }

        public override async Task<WopiResponse> PutUserInfo(PutUserInfoRequest putUserInfoRequest)
        {

            WopiResponse wopiResponse = null;
            var file = DocumentDBRepository<DetailedFileModel>.GetItem("Files", i => i.id.ToString() == putUserInfoRequest.ResourceId);

            // Check for null file
            if (file != null)
            {
                file.UserInfo = putUserInfoRequest.UserInfo;

                // Update the file in DocumentDB
                await DocumentDBRepository<FileModel>.UpdateItemAsync("Files", file.id.ToString(), (FileModel)file);

                // Return success
                return putUserInfoRequest.ResponseOK();
            }
            else
            {
                wopiResponse = putUserInfoRequest.ResponseNotFound();

            }
            return wopiResponse;
        }

        public override async Task<WopiResponse> PutFile(PutFileRequest putFileRequest)
        {
            WopiResponse wopiResponse = null;
            var file = DocumentDBRepository<DetailedFileModel>.GetItem("Files", i => i.id.ToString() == putFileRequest.ResourceId);

            // Check for null file
            if (file != null)
            {
                var inputStream = await putFileRequest.Content.ReadAsStreamAsync();

                // Ensure the file has a valid lock
                if (String.IsNullOrEmpty(file.LockValue))
                {
                    // If the file is 0 bytes, this is document creation
                    if (inputStream.Length == 0)
                    {
                        // Update the file in blob storage
                        var bytes = new byte[inputStream.Length];
                        inputStream.Read(bytes, 0, bytes.Length);
                        file.Size = bytes.Length;
                        await AzureStorageUtil.UploadFile(file.id.ToString(), file.Container, bytes);

                        // Update version
                        file.Version++;
                        await DocumentDBRepository<FileModel>.UpdateItemAsync("Files", file.id.ToString(), file);

                        // Return success 200
                        wopiResponse = putFileRequest.ResponseOK();
                    }
                    else
                    {
                        // File isn't locked...pass empty Lock in mismatch response
                        wopiResponse = putFileRequest.ResponseLockConflict(String.Empty, "File isn't locked");
                    }
                }
                else if (file.LockExpires != null && file.LockExpires < DateTime.Now)
                {
                    // File lock expired, so clear it out
                    file.LockValue = null;
                    file.LockExpires = null;
                    await DocumentDBRepository<FileModel>.UpdateItemAsync("Files", file.id.ToString(), file);

                    // File isn't locked...pass empty Lock in mismatch response
                    wopiResponse = putFileRequest.ResponseLockConflict(String.Empty, "File isn't locked");
                }
                else if (putFileRequest.Lock != file.LockValue)
                {
                    // File lock mismatch...pass Lock in mismatch response
                    wopiResponse = putFileRequest.ResponseLockConflict(file.LockValue, "Lock mismatch");
                }
                else
                {
                    // Update the file in blob storage
                    var bytes = new byte[inputStream.Length];
                    inputStream.Read(bytes, 0, bytes.Length);
                    file.Size = bytes.Length;
                    await AzureStorageUtil.UploadFile(file.id.ToString(), file.Container, bytes);

                    // Update version
                    file.Version++;
                    await DocumentDBRepository<FileModel>.UpdateItemAsync("Files", file.id.ToString(), (FileModel)file);

                    // Return success 200
                    wopiResponse = putFileRequest.ResponseOK();
                }
            }
            else
            {
                wopiResponse = putFileRequest.ResponseNotFound();
            }
            return wopiResponse;
        }

        public override Task<WopiResponse> DeleteFile(DeleteFileRequest deleteFileRequest)
        {
            return Task.FromResult<WopiResponse>(deleteFileRequest.ResponseNotImplemented());
        }
    }
}