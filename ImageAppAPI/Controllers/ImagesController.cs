using ImageApp.core;
using ImageApp.API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ImageApp.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImagesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ImagesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("all")] // GET api/images/all
        public async Task<ActionResult<IEnumerable<Image>>> GetAllImages()
        {
            var images = await _context.Images.OrderByDescending(i => i.CreatedAt).ToListAsync();
            return Ok(images);
        }

        [HttpPost("add")] // POST api/images/add
        public async Task<ActionResult<Image>> AddImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("File is required");

            if (file.Length > 10 * 1024 * 1024)
                return BadRequest("File size too large");

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);

            var image = new Image
            {
                Name = file.FileName,
                Data = memoryStream.ToArray(),
                ContentType = file.ContentType
            };

            _context.Images.Add(image);
            await _context.SaveChangesAsync();

            return Ok(image);
        }

        [HttpPut("update/{id}")] // PUT api/images/update/{id}
        public async Task<ActionResult<Image>> UpdateImage(int id, IFormFile file)
        {
            // Поиск существующего изображения по первичному ключу
            var existingImage = await _context.Images.FindAsync(id);
            if (existingImage == null)
                return NotFound();

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);

            // Обновление свойств существующей сущности
            existingImage.Name = file.FileName;
            existingImage.Data = memoryStream.ToArray();
            existingImage.ContentType = file.ContentType;

            await _context.SaveChangesAsync();

            return Ok(existingImage);
        }

        [HttpDelete("delete/{id}")] // DELETE api/images/delete/{id}
        public async Task<ActionResult> DeleteImage(int id)
        {
            var image = await _context.Images.FindAsync(id);
            if (image == null)
                return NotFound();

            _context.Images.Remove(image);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}