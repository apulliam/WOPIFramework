using System;
using System.Data.Entity;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Dx.WopiServerSql.Repository;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Dx.WopiServer.UnitTest
{
    [TestClass]
    public class UserRepositoryTests
    {
        private readonly string userId1 = "test1";
        private readonly string tenant1 = "DxWopi1";

        [TestMethod]
        public async Task TestAddGetAndDeleteUser()
        {
            var userRepository = new WopiUserRepository();
            WopiUser user;

            // Test AddUser
            await userRepository.AddUser(userId1,tenant1);
            using (var wopiContext = new WopiContext())
            {
                user = await wopiContext.Users.Where(u => u.UserId == userId1).FirstOrDefaultAsync();
                Assert.IsNotNull(user);
            }
         
            // Test GetUser
            user = await userRepository.GetUser(userId1);
            Assert.IsNotNull(user);

            // Test Add DuplicateUser
            try
            {
                await userRepository.AddUser(userId1, tenant1);
                Assert.Fail();
            }
            catch
            {

            }

            // Test Deleteuser
            await userRepository.DeleteUser(userId1);
            using (var wopiContext = new WopiContext())
            {
                user = await wopiContext.Users.Where(u => u.UserId == userId1).FirstOrDefaultAsync();
                Assert.IsNull(user);
            }
        }
    }
}
