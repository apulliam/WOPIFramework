using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Dx.WopiServerSql.Repository
{
    public class WopiUserRepository
    {
        public async Task AddUser(string userId, string tenant)
        {
            using (var wopiContext = new WopiContext())
            {
                wopiContext.Users.Add(new WopiUser() { UserId = userId, Tenant = tenant });
                await wopiContext.SaveChangesAsync();
            }
        }

        public async Task<WopiUser> GetUser(string userId)
        {
            using (var wopiContext = new WopiContext())
            {
                return await wopiContext.Users.Where(u => u.UserId == userId).FirstOrDefaultAsync();
            }
        }

        public async Task DeleteUser(string userId)
        {
            using (var wopiContext = new WopiContext())
            {
                var user = await wopiContext.Users.Where(u => u.UserId == userId).FirstOrDefaultAsync();
                if (user != null)
                {
                    wopiContext.Users.Remove(user);
                    await wopiContext.SaveChangesAsync();
                }
            }
        }
    }
}
