using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PictureFixer.Shared;

namespace PictureFixer.Server.Data
{
    public class EditableImageDbContext : DbContext
    {
        public EditableImageDbContext(DbContextOptions<EditableImageDbContext> options) : base(options) {}

        public DbSet<EditableImage> Images { get; set; }
    }
}