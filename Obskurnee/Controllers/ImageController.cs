﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Obskurnee.Models;
using Obskurnee.Services;
using Obskurnee.ViewModels;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Obskurnee.Controllers
{
    [Route("images")]
    [AllowAnonymous]
    public class ImageController : Controller
    {
        private readonly ApplicationDbContext _db;

        public ImageController(
            ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        [Route("{imageName}")]
        public async Task<IActionResult> Get(string imageName)
        {
            var image = await _db.Images.FirstOrDefaultAsync(i => i.FileName == imageName);
            if (image != null)
            {
                return File(image.FileContents, ExtensionToMime(image.Extension));
            }
            return await Task.FromResult((IActionResult)NotFound());
        }

        private string ExtensionToMime(string extension)
        {
            switch (extension)
            {
                case ".png":
                    return "image/png";
                case ".gif":
                    return "image/gif";
                case ".bmp":
                    return "image/bmp";
                case ".webp":
                    return "image/webp";
                case ".webm":
                    return "video/webm";
                case ".jpg":
                case ".jpeg":
                default:
                    return "image/jpeg";
            }
        }
    }
}