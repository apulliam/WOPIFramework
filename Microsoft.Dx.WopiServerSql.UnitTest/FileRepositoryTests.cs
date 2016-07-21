using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Dx.WopiServerSql.Repository;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Data.Entity;
using System.Net;

namespace Microsoft.Dx.WopiServer.UnitTest
{
    [TestClass]
    public class FileRepositoryTests
    {
        private readonly string fileName = "TestDocument.docx";
        private readonly string renamedFileName = "RenamedDocument.docx";
        private readonly string suggestedFileExtension = ".doc";
        private readonly string copyFileName = "TestDocument2.docx";
        private readonly string tenant1User1 = "test1";
        private readonly string tenant1User2 = "test2";
        private readonly string tenant1 = "DxWopi1";
        private readonly string tenant2 = "DxWopi2";
        private readonly string tenant2User1 = "test3";
        private readonly string lockId = "abc123";
        private readonly string wopiUserInfo = "Some data provided by WOPI client";


        [TestMethod]
        public async Task TestAddFileGetFileDeleteFile()
        {
            var fileRepository = new WopiFileRepository();
            using (var fileStream = new FileStream(fileName, FileMode.Open))
            {
                var fileName = Path.GetFileName(this.fileName);
                var wopiFile1 = await fileRepository.AddFile(tenant1User1, tenant1, fileStream, fileName);

                var wopiFile2 = await fileRepository.GetFileInfo(wopiFile1.FileId, tenant1User1);
                Assert.IsNotNull(wopiFile2);
                Assert.AreEqual(wopiFile1.FileId, wopiFile2.FileId);
                Assert.IsTrue(wopiFile2.FilePermissions.Count == 1);

                var contentResponse = await fileRepository.GetFileContent(wopiFile1.FileId, tenant1User1);
                Assert.AreEqual(HttpStatusCode.OK, contentResponse.Item1);
                using (var stream = contentResponse.Item2)
                {
                    Assert.IsNotNull(stream);
                    Assert.IsTrue(stream.Length > 0);
                }

                var deleteResponse = await fileRepository.DeleteFile(wopiFile1.FileId, tenant1User1);
                Assert.AreEqual(HttpStatusCode.OK, deleteResponse);
                using (var wopiContext = new WopiContext())
                {
                    var file = await wopiContext.Files.Where(f => f.FileId == wopiFile1.FileId).FirstOrDefaultAsync();
                    Assert.IsNull(file);
                    var filePermission = await wopiContext.FilePermissions.Where(f => f.FileId == wopiFile1.FileId).FirstOrDefaultAsync();
                    Assert.IsNull(filePermission);

                    contentResponse = await fileRepository.GetFileContent(wopiFile1.FileId, wopiFile1.Container);
                    Assert.AreEqual(HttpStatusCode.NotFound, contentResponse.Item1);
                    Assert.IsNull(contentResponse.Item2);
                }
            }
            
        }

        [TestMethod]
        public async Task TestAddFileGetFileTenantUserDeleteFile()
        {
            var fileRepository = new WopiFileRepository();
            using (var fileStream = new FileStream(fileName, FileMode.Open))
            {
                var fileName = Path.GetFileName(this.fileName);
                var wopiFile = await fileRepository.AddFile(tenant1User1, tenant1, fileStream, fileName);
                Assert.IsNotNull(wopiFile);

                var fileInfoResponse1 = await fileRepository.GetFileInfoByTenantUser(wopiFile.FileId, tenant1User2, tenant1);
                Assert.AreEqual(HttpStatusCode.OK, fileInfoResponse1.Item1);
                    
                var fileInfoResponse2 = await fileRepository.GetFileInfoByTenantUser(wopiFile.FileId, tenant2User1, tenant2);
                Assert.AreEqual(HttpStatusCode.Unauthorized, fileInfoResponse2.Item1);

                var deleteResponse = await fileRepository.DeleteFile(wopiFile.FileId, tenant1User1);
                Assert.AreEqual(HttpStatusCode.OK, deleteResponse);
            }
        }


        [TestMethod]
        public async Task TestLockFileAndExpiration()
        {
            var fileRepository = new WopiFileRepository();
            using (var fileStream = new FileStream(fileName, FileMode.Open))
            {
                var fileName = Path.GetFileName(this.fileName);
                var wopiFile = await fileRepository.AddFile(tenant1User1, tenant1, fileStream, fileName);

                // lock for 3 seconds
                var lockResponse = await fileRepository.LockFile(wopiFile.FileId, tenant1User1, lockId, null, 0.05);
                Assert.AreEqual(HttpStatusCode.OK, lockResponse.Item1);
                using (var wopiContext = new WopiContext())
                {
                    var file = await wopiContext.Files.Where(f => f.FileId == wopiFile.FileId).FirstOrDefaultAsync();
                    Assert.AreEqual(lockId, file.LockValue);
                    Assert.IsTrue(file.LockExpires.Value > DateTime.UtcNow);
                }

                // wait 5 seconds for lock to expire
                await Task.Delay(5000);
                var response2 = await fileRepository.GetLockStatus(wopiFile.FileId, tenant1User1);
                Assert.AreEqual(HttpStatusCode.OK, response2.Item1);

                var deleteResponse = await fileRepository.DeleteFile(wopiFile.FileId, tenant1User1);
                Assert.AreEqual(HttpStatusCode.OK, deleteResponse);
            }
        }

        [TestMethod]
        public async Task TestLockAndUnlockFile()
        {
            var fileRepository = new WopiFileRepository();
            using (var fileStream = new FileStream(fileName, FileMode.Open))
            {
                var fileName = Path.GetFileName(this.fileName);
                var wopiFile = await fileRepository.AddFile(tenant1User1, tenant1, fileStream, fileName);

                var lockResponse = await fileRepository.LockFile(wopiFile.FileId, tenant1User1, lockId, null, 0.10);
                Assert.AreEqual(HttpStatusCode.OK, lockResponse.Item1);

                using (var wopiContext = new WopiContext())
                {
                    var file = await wopiContext.Files.Where(f => f.FileId == wopiFile.FileId).FirstOrDefaultAsync();
                    Assert.AreEqual(lockId, file.LockValue);
                    Assert.IsTrue(file.LockExpires.Value > DateTime.UtcNow);
                }

                var unlockResponse = await fileRepository.UnlockFile(wopiFile.FileId, tenant1User1, lockId);
                Assert.AreEqual(HttpStatusCode.OK, unlockResponse.Item1);

                using (var wopiContext = new WopiContext())
                {
                    var file = await wopiContext.Files.Where(f => f.FileId == wopiFile.FileId).FirstOrDefaultAsync();
                    Assert.IsNull(file.LockValue);
                }
                var deleteResponse = await fileRepository.DeleteFile(wopiFile.FileId, tenant1User1);
                Assert.AreEqual(HttpStatusCode.OK, deleteResponse);
            }
        }

        [TestMethod]
        public async Task TestRename()
        {
            var fileRepository = new WopiFileRepository();
            var fileName = Path.GetFileName(this.fileName);
            using (var fileStream = new FileStream(this.fileName, FileMode.Open))
            {
                var wopiFile = await fileRepository.AddFile(tenant1User1, tenant1, fileStream, fileName);
                var result = await fileRepository.RenameFile(wopiFile.FileId, tenant1User1, lockId, renamedFileName);
                Assert.AreEqual(HttpStatusCode.OK, result);
                using (var wopiContext = new WopiContext())
                {
                    var file = await wopiContext.Files.Where(f => f.FileId == wopiFile.FileId).FirstOrDefaultAsync();
                    Assert.AreEqual(renamedFileName, Path.GetFileNameWithoutExtension(file.FileName));
                }
                var deleteResponse = await fileRepository.DeleteFile(wopiFile.FileId, tenant1User1);
                Assert.AreEqual(HttpStatusCode.OK, deleteResponse);
            }

            //await userRepository.DeleteUser(tenant1User1);
        }

        [TestMethod]
        public async Task TestSaveWopiUserInfo()
        {
            var fileName = Path.GetFileName(this.fileName);
            using (var fileStream = new FileStream(this.fileName, FileMode.Open))
            {
                var fileRepository = new WopiFileRepository();

                var wopiFile = await fileRepository.AddFile(tenant1User1, tenant1, fileStream, fileName);

                await fileRepository.SaveWopiUserInfo(wopiFile.FileId, tenant1User1, wopiUserInfo);
                using (var wopiContext = new WopiContext())
                {
                    var filePermission = await wopiContext.FilePermissions.Where(fp => fp.UserId == tenant1User1 && fp.FileId == wopiFile.FileId).FirstOrDefaultAsync();
                    Assert.AreEqual(wopiUserInfo, filePermission.UserInfo);
                }

                var deleteResponse = await fileRepository.DeleteFile(wopiFile.FileId, tenant1User1);
                Assert.AreEqual(HttpStatusCode.OK, deleteResponse);
            }
        }

        [TestMethod]
        public async Task TestCreateCopy()
        {
            var fileRepository = new WopiFileRepository();
            var fileName = Path.GetFileName(this.fileName);
            using (var fileStream = new FileStream(this.fileName, FileMode.Open))
            {
                var originalFile = await fileRepository.AddFile(tenant1User1, tenant1, fileStream, fileName);

                // Test owner copy
                var ownerCopyResult = await fileRepository.CreateCopy(originalFile.FileId, tenant1User1, copyFileName);
                Assert.AreEqual(HttpStatusCode.OK,ownerCopyResult.Item1);
                Assert.IsNotNull(ownerCopyResult.Item2);

                // Test copy to an existing file without overwrite
                var result = await fileRepository.CreateCopy(originalFile.FileId, tenant1User1, copyFileName);
                Assert.AreEqual(HttpStatusCode.Conflict, result.Item1);
                
                // obtain access rights to original file for tenant1User2 in same tenant
                var result1 = await fileRepository.GetFileInfoByTenantUser(ownerCopyResult.Item2.FileId, tenant1User2, tenant1);
                Assert.AreEqual(HttpStatusCode.OK, result1.Item1);
              
                // Get lock for tenantUser1
                var result2 = await fileRepository.LockFile(ownerCopyResult.Item2.FileId, tenant1User1 , lockId, null);
                Assert.AreEqual(HttpStatusCode.OK, result2.Item1);

                // Test copy to an existing file by tenantUser2 when locked by tenant1User1
                var result3 = await fileRepository.CreateCopy(ownerCopyResult.Item2.FileId, tenant1User2, copyFileName, true);
                Assert.AreEqual(HttpStatusCode.Conflict, result3.Item1);

                // release lock for tenant1User2 
                var result4 = await fileRepository.UnlockFile(ownerCopyResult.Item2.FileId, tenant1User1, lockId);
                Assert.AreEqual(HttpStatusCode.OK, result4.Item1);

                // Try copy again on existing file with overwrite
                var tenantCopyResult = await fileRepository.CreateCopy(ownerCopyResult.Item2.FileId, tenant1User2, copyFileName, true);
                Assert.AreEqual(HttpStatusCode.OK, tenantCopyResult.Item1);

                var deleteResponse = await fileRepository.DeleteFile(originalFile.FileId, tenant1User1);
                Assert.AreEqual(HttpStatusCode.OK, deleteResponse);
                deleteResponse = await fileRepository.DeleteFile(tenantCopyResult.Item2.FileId, tenant1User2);
                Assert.AreEqual(HttpStatusCode.OK, deleteResponse);
            }
        }

        [TestMethod]
        public async Task TestCreateCopySuggested()
        {
            var fileRepository = new WopiFileRepository();
            var fileName = Path.GetFileName(this.fileName);
            using (var fileStream = new FileStream(this.fileName, FileMode.Open))
            {
                var wopiFile = await fileRepository.AddFile(tenant1User1, tenant1, fileStream, fileName);
                
                // Test suggested file extension
                var result1 = await fileRepository.CreateCopySuggested(wopiFile.FileId, tenant1User1, suggestedFileExtension);
                Assert.AreEqual(HttpStatusCode.OK, result1.Item1);
                Assert.IsNotNull(result1.Item2);

                // test full file name no conflict
                var result2 = await fileRepository.CreateCopySuggested(wopiFile.FileId, tenant1User1, copyFileName);
                Assert.AreEqual(HttpStatusCode.OK, result2.Item1);

                // test full file name conflict
                var result3 = await fileRepository.CreateCopySuggested(wopiFile.FileId, tenant1User1, copyFileName);
                Assert.AreEqual(HttpStatusCode.OK, result3.Item1);


                // test second full file name conflict
                var result4 = await fileRepository.CreateCopySuggested(wopiFile.FileId, tenant1User1, copyFileName);
                Assert.AreEqual(HttpStatusCode.OK, result4.Item1);

                var deleteResponse = await fileRepository.DeleteFile(wopiFile.FileId, tenant1User1);
                Assert.AreEqual(HttpStatusCode.OK, deleteResponse);
                deleteResponse = await fileRepository.DeleteFile(result1.Item2.FileId, tenant1User1);
                Assert.AreEqual(HttpStatusCode.OK, deleteResponse);
                deleteResponse = await fileRepository.DeleteFile(result2.Item2.FileId, tenant1User1);
                Assert.AreEqual(HttpStatusCode.OK, deleteResponse);
                deleteResponse = await fileRepository.DeleteFile(result3.Item2.FileId, tenant1User1);
                Assert.AreEqual(HttpStatusCode.OK, deleteResponse);
                deleteResponse = await fileRepository.DeleteFile(result4.Item2.FileId, tenant1User1);
                Assert.AreEqual(HttpStatusCode.OK, deleteResponse);
            }
        }

    }
}
