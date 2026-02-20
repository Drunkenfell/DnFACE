using System;
using System.ComponentModel.DataAnnotations;

namespace ACE.Database.Models.World
{
    public partial class ContentUnlock
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; }

        // JSON payload defining unlock behavior (see HarmonyPlus README)
        public string Payload { get; set; }

        public bool Enabled { get; set; }

        public DateTime LastModified { get; set; }
    }
}
