using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace Microsoft.Dx.WopiServerSql.Repository
{
    public class WopiContext : DbContext
    {
        public DbSet<WopiFile> Files { get; set; }
        public DbSet<WopiFilePermission> FilePermissions { get; set; }
        
    }
}