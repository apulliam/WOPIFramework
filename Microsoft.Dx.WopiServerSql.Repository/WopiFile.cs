using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.Dx.Wopi.Models;

namespace Microsoft.Dx.WopiServerSql.Repository
{
    public class WopiFile
    {
        [Key]
        public string FileId { get; set; }

        public string LockValue { get; set; }

        public DateTime? LockExpires { get; set; }

        [Required]
        public string OwnerId { get; set; }

        [Required]
        public string FileName { get; set; }

        [Required]
        public string FileExtension { get; set; }

        [Required]
        public string Container { get; set; }

        [Required]
        public long Size { get; set; }

        [Required]
        public int Version { get; set; }

     
        [Required]
        public DateTimeOffset LastModifiedTime { get; set; }

        [Required]
        public string LastModifiedUser { get; set; }

        public virtual ICollection<WopiFilePermission> FilePermissions { get; set; }

        public bool IsLocked
        {
            get
            {
                return LockValue != null && LockExpires > DateTime.UtcNow;
            }
        }

        public bool IsSameLock(string lockId)
        {
            return (LockValue == lockId);
        }

        public void Lock(string lockId, double lockDurationMinutes)
        {
            LockValue = lockId;
            LockExpires = DateTime.UtcNow.AddMinutes(lockDurationMinutes);
        }

        public void Unlock()
        {
            LockValue = null;
        }

    }
}
