using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using Lfm.Domain.Common.Extensions;
using LFM.Domain.Read.Providers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace LFM.Web.Mvc.Controllers
{
    [Route("image")]
    public class ImagesController : Controller
    {
        private readonly IMentorProfileProvider _mentorProfileProvider;
        private readonly IWebHostEnvironment _environment;

        public ImagesController(
            IMentorProfileProvider mentorProfileProvider, 
            IWebHostEnvironment environment)
        {
            _mentorProfileProvider = mentorProfileProvider;
            _environment = environment;
        }

        [HttpGet("mentor/avatar")]
        public async Task<IActionResult> GetMentorAvatar(int? mentorId)
        {
            if (!mentorId.HasValue)
            {
                return ReturnDefaultMentorAvatar();
            }

            var avatar = await _mentorProfileProvider.GetAvatar(mentorId ?? User.GetId());

            if (avatar?.Length > 0)
            {
                return File(avatar, "image/jpeg", $"mentor_avatar");
            }
            
            return ReturnDefaultMentorAvatar();
        }
        
        [NonAction]
        private IActionResult ReturnDefaultMentorAvatar()
        {
            Image im = Image.FromFile($"{_environment.WebRootPath}/images/default-avatar.png");

            MemoryStream ms = new MemoryStream();
            im.Save(ms, im.RawFormat);

            return File(ms.ToArray(), "image/png", "default-avatar.png");
        }
    }
}