using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Dx.WopiServerSql.Repository
{
    public class WopiFileLock
    {
        private string _lockId;
        public bool IsLockedExpired
        {
            get
            {
                return LockExpires >= DateTime.UtcNow;
            }
        }

        public DateTime LockExpires { get; private set; } 

        public bool IsSameLock(string lockId)
        {
            return (_lockId == lockId);
        }
        internal WopiFileLock(string lockId, DateTime lockExpiration)
        {
            _lockId = lockId;
            LockExpires = lockExpiration;
        }
    }
}
