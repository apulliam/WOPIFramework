using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Data.Entity;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Net;
using Microsoft.WindowsAzure.Storage.File;

namespace Microsoft.Dx.WopiServerSql.Repository
{
    public class WopiFileRepository 
    {
        /// <summary>
        /// Adds a file to the user's tenant container in the repository.  An owner file permission record is also created allowing all WOPI operations.
        /// </summary>
        /// <param name="userId">UserID of the file owner</param>
        /// <param name="stream">Stream containing file contents</param>
        /// <param name="fileName">Name of file with extension</param>
        /// <returns>Returns a string with unique FileID identifier, which is unique across the repository</returns>
        public async Task<WopiFile> AddFile(string userId, string tenant, Stream stream, string fileName)
        {
            var fileId = Guid.NewGuid().ToString();
            CloudBlockBlob blockBlob = null;
            try
            {
                using (var wopiContext = new WopiContext())
                {
                    var existingFiles = await wopiContext.Files.Where(f => f.FileName == fileName && f.Container == tenant).ToListAsync();

                    var storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));
                    var blobClient = storageAccount.CreateCloudBlobClient();
                    var containerName = tenant.ToLower(); // blob containers must be lowercase
                    var container = blobClient.GetContainerReference(containerName);
                    await container.CreateIfNotExistsAsync();
                    blockBlob = container.GetBlockBlobReference(fileId);
                    await blockBlob.UploadFromStreamAsync(stream);

                    int newVersion = 1;

                    if (existingFiles.Count() > 0)
                    {
                        var versions = existingFiles.Select(file => file.Version);
                        var maxVersion = versions.Max();
                        newVersion = maxVersion + 1;
                    }

                    var wopiFile = CreateOwnerRecords(fileName, userId, blockBlob, newVersion);
                    wopiContext.Files.Add(wopiFile);
                    await wopiContext.SaveChangesAsync();
                    return wopiFile;
                }

            }
            catch (Exception ex)
            {
                if (blockBlob != null && await blockBlob.ExistsAsync())
                    await blockBlob.DeleteAsync();
                throw;
            }
        }

        private WopiFile CreateOwnerRecords(string fileName, string userId, CloudBlockBlob blockBlob, int version)
        {
            var wopiFile = new WopiFile()
            {
                FileId = blockBlob.Name,
                FileName = fileName,
                FileExtension = Path.GetExtension(fileName),
                OwnerId = userId.ToLower(),
                Size = blockBlob.Properties.Length,
                Container = blockBlob.Container.Name,
                Version = version,
                LastModifiedTime = blockBlob.Properties.LastModified.Value,
                LastModifiedUser = userId.ToLower(),
                FilePermissions = new HashSet<WopiFilePermission>()
            };

            wopiFile.FilePermissions.Add(new WopiFilePermission()
            {
                File = wopiFile,
                ReadOnly = false,
                UserCanWrite = true,
                UserCanRename = true,
                UserCanNotWriteRelative = false,
                UserCanPresent = true,
                UserCanAttend = true,
                UserId = userId
            });
            return wopiFile;
        }

        /// <summary>
        /// Deletes the specified file from the repository if the user has appropriate rights.  Currenlty only the the owner of the file is allowed to delete.
        /// </summary>
        /// <param name="fileId">ID file to delete</param>
        /// <param name="userId">ID of user with owner rights on file</param>
        /// <returns></returns>
        public async Task<HttpStatusCode> DeleteFile(string fileId, string userId)
        {
            using (var wopiContext = new WopiContext())
            {
                var file = await wopiContext.Files.Where(f => f.FileId == fileId).Include("FilePermissions").FirstOrDefaultAsync();

                if (file == null)
                    return HttpStatusCode.NotFound;

                if (file.OwnerId != userId)
                    return HttpStatusCode.Unauthorized;

                //file.FilePermissions.ToList().ForEach(fp => wopiContext.FilePermissions.Remove(fp));
                wopiContext.Files.Remove(file);

                var storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));
                var blobClient = storageAccount.CreateCloudBlobClient();
                var container = blobClient.GetContainerReference(file.Container);
                if (await container.ExistsAsync())
                {
                    var blockBlob = container.GetBlockBlobReference(file.FileId);
                    if (await blockBlob.ExistsAsync())
                    {
                        await blockBlob.DeleteAsync();
                        await wopiContext.SaveChangesAsync();
                        return HttpStatusCode.OK;
                    }
                }
                return HttpStatusCode.NotFound;
            }
        }
        /// <summary>
        /// Replaces file content with data in provide file stream if user has appropriate rights.  Currently only the owner is allowed to UpdateFileContents.
        /// </summary>
        /// <param name="fileId"></param>
        /// <param name="userId"></param>
        /// <param name="lockValue"></param>
        /// <param name="stream"></param>
        /// <returns></returns>
        public async Task<Tuple<HttpStatusCode,string,string>> UpdateFileContent(string fileId,string userId, string lockValue, Stream stream)
        {
            using (var wopiContext = new WopiContext())
            {
                var filePermission = await wopiContext.FilePermissions.Where(fp => fp.FileId == fileId && fp.UserId == userId).Include("File").FirstOrDefaultAsync();
                if (filePermission == null)
                    return new Tuple<HttpStatusCode,string,string>(HttpStatusCode.NotFound, null, null);

                WopiFile file = filePermission.File;
                if (!file.IsLocked)
                {
                    if (file.Size > 0)
                        return new Tuple<HttpStatusCode,string,string>(HttpStatusCode.Conflict, string.Empty,null);
                }
                else
                {
                    if (!file.IsSameLock(lockValue))
                        return new Tuple<HttpStatusCode,string,string>(HttpStatusCode.Conflict, file.LockValue, null);
                }

               

                var storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));
                var blobClient = storageAccount.CreateCloudBlobClient();
                var containerName = file.Container;
                var container = blobClient.GetContainerReference(containerName);
                await container.CreateIfNotExistsAsync();
                var blockBlob = container.GetBlockBlobReference(fileId);
                await blockBlob.UploadFromStreamAsync(stream);

                file.Size = blockBlob.Properties.Length;
                file.LastModifiedTime = blockBlob.Properties.LastModified.Value;
                file.LastModifiedUser = userId.ToLower();


                await wopiContext.SaveChangesAsync();
                return new Tuple<HttpStatusCode,string,string>(HttpStatusCode.OK, null, file.Version.ToString());
            }
        }

        /// <summary>
        /// Returns all files in SQL Repository by tenant ID which
        /// is the stored in Container filed of WopiFile and also
        /// used for Azure Blob container name.
        /// </summary>
        /// <param name="tenant">tenant name to use when searching for files</param>
        /// <returns></returns>
        public async Task<List<WopiFile>> GetFilesByTenant(string tenant)
        {
            using (var wopiContext = new WopiContext())
            {
                return await wopiContext.Files.Where(f => f.Container == tenant).OrderBy(f => f.FileName).OrderBy(f => f.Version).ToListAsync();
            }
        }

        public async Task<Tuple<HttpStatusCode, WopiFile>> GetFileInfoByTenantUser(string fileId, string userId, string tenant)
        {
            using (var wopiContext = new WopiContext())
            {
                var file = await wopiContext.Files.Where(f => f.FileId == fileId).Include("FilePermissions").FirstOrDefaultAsync();
                if (file == null)
                    return new Tuple<HttpStatusCode, WopiFile>(HttpStatusCode.NotFound,null);

                var filePermission = file.FilePermissions.Where(fp => fp.UserId == userId).FirstOrDefault();

                // If the user is in the same tenant as Add new file permission with default tenant member permissions
                if (filePermission == null)
                {
                    if (file.Container != tenant.ToLower())
                        return new Tuple<HttpStatusCode, WopiFile>(HttpStatusCode.Unauthorized, null);

                    filePermission = new WopiFilePermission()
                    {
                        File = file,
                        ReadOnly = true,
                        RestrictedWebViewOnly = true,
                        UserCanAttend = true,
                        UserCanNotWriteRelative = true,
                        UserCanPresent = false,
                        UserCanRename = false,
                        UserCanWrite = false,
                        WebEditingDisabled = true,
                        UserId = userId
                        
                    };

                    file.FilePermissions.Add(filePermission);
                }

                await wopiContext.SaveChangesAsync();

                // Only return this user's file permissions
                file.FilePermissions.Clear();
                file.FilePermissions.Add(filePermission);

                return new Tuple<HttpStatusCode,WopiFile>(HttpStatusCode.OK, file);
            }
        }



        /// <summary>
        /// Gets file from SQL Repository by fileId where the user already has rights.
        /// </summary>
        /// <param name="fileId">Unique ID for file in repository</param>
        /// <param name="userId">User ID to check for permissions to file</param>
        /// <returns>Return WopiFile if file is found and user has rights, null otherwise</returns>
        public async Task<WopiFile> GetFileInfo(string fileId, string userId)
        {
            using (var wopiContext = new WopiContext())
            {
                var file = await wopiContext.Files.Where(f => f.FileId == fileId && f.FilePermissions.Any(fp => fp.UserId == userId)).Include("FilePermissions").FirstOrDefaultAsync();
                
                return file;
            }
        }


        public async Task<Tuple<HttpStatusCode,Stream,string>> GetFileContent(string fileId, string userId)
        {
            var wopiFile = await GetFileInfo(fileId, userId);
            if (wopiFile != null)
            {
                var storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));
                var blobClient = storageAccount.CreateCloudBlobClient();
                var container = blobClient.GetContainerReference(wopiFile.Container);
                if (await container.ExistsAsync())
                {
                    var blockBlob = container.GetBlobReference(fileId);
                    if (await blockBlob.ExistsAsync())
                    {
                        var stream = await blockBlob.OpenReadAsync();
                        return new Tuple<HttpStatusCode,Stream,string>(HttpStatusCode.OK, stream, wopiFile.Version.ToString());
                    }
                }
            }
            return new Tuple<HttpStatusCode,Stream,string>(HttpStatusCode.NotFound,null, null);
        }
        

        public async Task<Tuple<HttpStatusCode,string,string>> LockFile(string fileId, string userId, string lockId, string oldLockId, double lockDurationMinutes = 30)
        {
            if (string.IsNullOrEmpty(lockId))
                return new Tuple<HttpStatusCode,string,string>(HttpStatusCode.BadRequest, null, null);

            using (var wopiContext = new WopiContext())
            {
                var file = await wopiContext.Files.Where(f => f.FileId == fileId && f.FilePermissions.Any(fp => fp.UserId == userId)).FirstOrDefaultAsync();
                if (file == null)
                    return new Tuple<HttpStatusCode,string,string>(HttpStatusCode.NotFound,null,null);

                if (oldLockId != null)
                {
                    if (file.IsLocked)
                    {
                        if (!file.IsSameLock(oldLockId))
                            return new Tuple<HttpStatusCode,string,string>(HttpStatusCode.Conflict, file.LockValue, null);
                    }
                    else
                    {
                        return new Tuple<HttpStatusCode,string,string>(HttpStatusCode.Conflict, string.Empty,null);
                    }
                }
                else
                {
                    if (file.IsLocked && !file.IsSameLock(lockId))
                        return new Tuple<HttpStatusCode,string,string>(HttpStatusCode.Conflict, file.LockValue,null);

                }

                file.Lock(lockId, lockDurationMinutes);
                await wopiContext.SaveChangesAsync();
                return new Tuple<HttpStatusCode,string,string>(HttpStatusCode.OK, null,file.Version.ToString());
            }
        }
        

        public async Task<Tuple<HttpStatusCode,string>> GetLockStatus(string fileId,string userId)
        {
            using (var wopiContext = new WopiContext())
            {
                var file = await wopiContext.Files.Where(f => f.FileId == fileId  && f.FilePermissions.Any(fp => fp.UserId == userId)).FirstOrDefaultAsync();
                if (file == null)
                    return new Tuple<HttpStatusCode,string>(HttpStatusCode.NotFound, null);

                if (file.IsLocked)
                    return new Tuple<HttpStatusCode, string>(HttpStatusCode.OK, file.LockValue);
                else
                    return new Tuple<HttpStatusCode, string>(HttpStatusCode.OK,string.Empty);
            }
        }

        public async Task<Tuple<HttpStatusCode,string,string>> UnlockFile(string fileId, string userId, string lockId)
        {
            if (string.IsNullOrEmpty(lockId))
                return new Tuple<HttpStatusCode,string,string>(HttpStatusCode.BadRequest, null, null);

            using (var wopiContext = new WopiContext())
            {
                var wopiFile = await wopiContext.Files.Where(f => f.FileId == fileId  && f.FilePermissions.Any(fp => fp.UserId == userId)).FirstOrDefaultAsync();
                if (wopiFile == null)
                    return new Tuple<HttpStatusCode,string,string>(HttpStatusCode.NotFound, null, null);

                if (wopiFile.IsLocked)
                {
                    if (!wopiFile.IsSameLock(lockId))
                        return new Tuple<HttpStatusCode,string,string>(HttpStatusCode.Conflict, wopiFile.LockValue, null);
                }
                else
                    return new Tuple<HttpStatusCode,string,string>(HttpStatusCode.Conflict, string.Empty, null);

                wopiFile.Unlock();
                await wopiContext.SaveChangesAsync();
                return new Tuple<HttpStatusCode,string,string>(HttpStatusCode.OK, null, wopiFile.Version.ToString());
            }
        }

        public async Task<Tuple<HttpStatusCode,string>> RenameFile(string fileId, string userId, string lockId, string newBaseFileName)
        {
            using (var wopiContext = new WopiContext())
            {
                var file = await wopiContext.Files.Where(f => f.FileId == fileId  && f.FilePermissions.Any(fp => fp.UserId == userId)).FirstOrDefaultAsync();
                if (file == null)
                    return new Tuple<HttpStatusCode, string>(HttpStatusCode.NotFound, null);

                if (file.IsLocked && !file.IsSameLock(lockId))
                    return new Tuple<HttpStatusCode, string>(HttpStatusCode.Conflict, file.LockValue);

                var newFileName = newBaseFileName + file.FileExtension;
                if (newFileName.Length > 512)
                    return new Tuple<HttpStatusCode, string>(HttpStatusCode.BadRequest,null);
                var existingFile = await wopiContext.Files.Where(f => f.FileName == newFileName).FirstOrDefaultAsync();
                if (existingFile != null)
                    return new Tuple<HttpStatusCode, string>(HttpStatusCode.BadRequest, "File already exists");

                file.FileName = newFileName;
                await wopiContext.SaveChangesAsync();
                return new Tuple<HttpStatusCode, string>(HttpStatusCode.OK, newBaseFileName);
            }
        }
    
        public async Task<Tuple<HttpStatusCode, WopiFile, string>> CreateCopy(string fileId, string userId, string copyName, bool overwrite = false)
        {
            using (var wopiContext = new WopiContext())
            {
                var originalFile = await wopiContext.Files.Where(f => f.FileId == fileId  && f.FilePermissions.Any(fp => fp.UserId == userId)).FirstOrDefaultAsync();
                if (originalFile == null)
                    return new Tuple<HttpStatusCode, WopiFile,string>(HttpStatusCode.NotFound,null,null);

                if (!FileNameUtil.IsValidFileName(copyName))
                    return new Tuple<HttpStatusCode, WopiFile,string>(HttpStatusCode.BadRequest, null, null);

                var existingFile = await wopiContext.Files.Where(f => f.FileName == copyName).FirstOrDefaultAsync();
                if (existingFile != null)
                {
                    if (!overwrite)
                        return new Tuple<HttpStatusCode, WopiFile,string>(HttpStatusCode.Conflict, null, existingFile.LockValue);
                    if (existingFile.IsLocked)
                        return new Tuple<HttpStatusCode, WopiFile,string>(HttpStatusCode.Conflict, null, existingFile.LockValue);
                }

                var storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));
                var blobClient = storageAccount.CreateCloudBlobClient();

                var newWopiFile = await CreateCopyInternal(blobClient, wopiContext, originalFile, copyName, userId);
                wopiContext.Files.Add(newWopiFile);

                if (existingFile != null)
                {
                    wopiContext.Files.Remove(existingFile);
                }

                await wopiContext.SaveChangesAsync();

                if (existingFile != null)
                {
                    // Do we need to explicity delete FilePermissions?
                    var container = blobClient.GetContainerReference(existingFile.Container);
                    if (await container.ExistsAsync())
                    {
                        var blockBlob = container.GetBlockBlobReference(existingFile.FileId);
                        if (await blockBlob.ExistsAsync())
                        {
                            await blockBlob.DeleteAsync();
                        }
                    }
                }

                return new Tuple<HttpStatusCode, WopiFile,string>(HttpStatusCode.OK, newWopiFile,null);
            }
        }

        private async Task<WopiFile> CreateCopyInternal(CloudBlobClient blobClient, WopiContext wopiContext, WopiFile originalFile, string newFileName, string userId)
        {
            CloudBlockBlob newBlob = null;
            WopiFile newWopiFile = null;
            try
            {
                var newFileId = Guid.NewGuid().ToString();
               
                var container = blobClient.GetContainerReference(originalFile.Container);
                var existingBlob = container.GetBlockBlobReference(originalFile.FileId);
                newBlob = container.GetBlockBlobReference(newFileId);
                var copyOperation = await newBlob.StartCopyAsync(existingBlob);
                await WaitForCopyAsync(newBlob);

                newWopiFile = CreateOwnerRecords(newFileName, userId, newBlob, 1);
                

            
            }
            catch (Exception ex)
            {
                if (newBlob != null && await newBlob.ExistsAsync())
                    await newBlob.DeleteAsync();
                throw;
            }
            return newWopiFile;

        }

        /// <summary>
        /// Creates a copy of the specified file using an generated file name,
        /// which is returned as the output.  
        /// </summary>
        /// <param name="fileId">Unique ID of source file</param>
        /// <param name="userId">UserID of user the initiating copy.  This will be the owner of the file copy.</param>
        /// <returns>Name of the new file or null if the file was not found or the specified user does not have permissions to the file</returns>
        public async Task<Tuple<HttpStatusCode, WopiFile>> CreateCopySuggested(string fileId, string userId, string suggestedCopyName)
        {
            using (var wopiContext = new WopiContext())
            {
                var originalFile = await wopiContext.Files.Where(f => f.FileId == fileId && f.FilePermissions.Any(fp => fp.UserId == userId)).FirstOrDefaultAsync();
                if (originalFile == null)
                    return new Tuple<HttpStatusCode, WopiFile>(HttpStatusCode.NotFound, null);

                if (suggestedCopyName.StartsWith("."))
                    suggestedCopyName = Path.GetFileNameWithoutExtension(originalFile.FileName) + suggestedCopyName;

                var newFileName = FileNameUtil.MakeValidFileName(suggestedCopyName);
                WopiFile existingFile = null;
                do
                {
                    existingFile = await wopiContext.Files.Where(f => f.FileName == newFileName).FirstOrDefaultAsync();

                    if (existingFile != null)
                    {
                        newFileName = Path.GetFileNameWithoutExtension(newFileName) + "-copy" + Path.GetExtension(newFileName);
                    }
                }
                while (existingFile != null);

                var storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("StorageConnectionString"));
                var blobClient = storageAccount.CreateCloudBlobClient();
                var newFile = await CreateCopyInternal(blobClient,wopiContext, originalFile, newFileName, userId);
                wopiContext.Files.Add(newFile);
                await wopiContext.SaveChangesAsync();
                return new Tuple<HttpStatusCode, WopiFile>(HttpStatusCode.OK, newFile);
            }
        }

        public static async Task WaitForCopyAsync(CloudBlockBlob file)
        {
            bool copyInProgress = true;
            while (copyInProgress)
            {
                await Task.Delay(1000);
                await file.FetchAttributesAsync();
                copyInProgress = (file.CopyState.Status == CopyStatus.Pending);
            }
        }

        public async Task<HttpStatusCode> SaveWopiUserInfo(string fileId, string userId, string wopiUserInfo)
        {
            using (var wopiContext = new WopiContext())
            {
                var filePermission = await wopiContext.FilePermissions.Where(fp => fp.FileId == fileId && fp.UserId == userId).FirstOrDefaultAsync();
                if (filePermission == null)
                    return HttpStatusCode.NotFound;
                
                filePermission.UserInfo = wopiUserInfo;
                wopiContext.SaveChanges();
               
                return HttpStatusCode.OK;
            }
        }
    }
}
