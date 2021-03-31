using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PictureFixer.Shared;
using PictureFixer.Server.Data;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;

namespace PictureFixer.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EditableImageController : ControllerBase
    {
        private readonly EditableImageDbContext _context;
        private readonly ILogger<EditableImageController> _logger;
        private readonly HttpClient _http;

        public EditableImageController(ILogger<EditableImageController> logger, EditableImageDbContext context, HttpClient http)
        {
            _logger = logger;
            _context = context;
            _http = http;
        }

        [HttpGet]
        public async Task<IEnumerable<EditableImage>> GetAllImages()
        {
            return await _context.Images.OrderByDescending(image => image.CreatedAt).ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<EditableImage> GetImageById(int id)
        {
            var image = await _context.Images.FirstOrDefaultAsync(p => p.Id == id);
            if (image == null)
            {
                return null;
            }

            return image;
        }

        [HttpGet("{id}/contents")]
        [ResponseCache(Duration = 60)]
        public async Task<IActionResult> GetImageContentsById(int id)
        {
            var image = await _context.Images.FirstOrDefaultAsync(p => p.Id == id);
            var response = await _http.GetAsync(image.CdnLocation);
            return new FileStreamResult(
                await response.Content.ReadAsStreamAsync(),
                response.Content.Headers.ContentType?.ToString());
        }

        [HttpPost]
        public async Task<EditableImage> AddImage(EditableImage image)
        {
            _context.Add(image);
            await _context.SaveChangesAsync();

            return image;
        }

        [HttpPut("{id}")]
        public async Task<EditableImage> UpdateImage(EditableImage image)
        {
            _context.Update(image);
            await _context.SaveChangesAsync();

            return image;
        }
    }
}
