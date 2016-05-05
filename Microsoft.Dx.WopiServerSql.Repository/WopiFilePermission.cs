using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace Microsoft.Dx.WopiServerSql.Repository
{
    public class WopiFilePermission
    {
        public virtual WopiFile File { get; set; }

        [Key, Column(Order = 0)]
        public string UserId { get; set; }

        [Key, Column(Order = 1)]
        public string FileId { get; set; }

        [Required]
        public bool ReadOnly
        {
            get;
            set;
        }

        [Required]
        public bool RestrictedWebViewOnly
        {
            get;
            set;
        }

        [Required]
        public bool UserCanAttend
        {
            get;
            set;
        }

        [Required]
        public bool UserCanNotWriteRelative
        {
            get;
            set;
        }

        [Required]
        public bool UserCanPresent
        {
            get;
            set;
        }

        [Required]
        public bool UserCanRename
        {
            get;
            set;
        }

        [Required]
        public bool UserCanWrite
        {
            get;
            set;
        }

        [Required]
        public bool WebEditingDisabled
        {
            get;
            set;
        }

        public string UserInfo { get; set; }
    }
}