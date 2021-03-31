using System;
using System.ComponentModel.DataAnnotations;

namespace PictureFixer.Shared
{
    public class EditableImage
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        [Required]
        public string Description { get; set; }

        public string CdnLocation { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string RelativeUrl => $"api/EditableImage/{Id}/contents";

        public DateTime ModifiedAt { get; set; }
    }
}
