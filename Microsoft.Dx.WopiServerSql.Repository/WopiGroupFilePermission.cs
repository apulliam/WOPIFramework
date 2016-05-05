using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace Microsoft.Dx.WopiServerSql.Repository
{
    public class WopiGroupFilePermission
    {
        public virtual WopiFile File { get; set; }

        [Key, Column(Order = 0)]
        public string GroupId { get; set; }

        [Key, Column(Order = 1)]
        public string FileId { get; set; }

        public bool ReadOnly
        {
            get;
            set;
        }

        public bool RestrictedWebViewOnly
        {
            get;
            set;
        }

        public bool UserCanAttend
        {
            get;
            set;
        }
     
        public bool UserCanNotWriteRelative
        {
            get;
            set;
        }

        public bool UserCanPresent
        {
            get;
            set;
        }

        public bool UserCanRename
        {
            get;
            set;
        }

        public bool UserCanWrite
        {
            get;
            set;
        }

        public bool WebEditingDisabled
        {
            get;
            set;
        }
    }
}