using System;
using System.Linq;
using System.Threading.Tasks;
using LFM.Core.Common.Data;
using Lfm.Core.Common.Web.Configurations;
using Lfm.Core.Common.Web.SessionAlerts;
using LFM.DataAccess.DB.Core.Context;
using LFM.Domain.Read.Providers;
using Lfm.Domain.ReadModels.SearchModels;
using Lfm.Web.Mvc.App.StaticServices;
using Lfm.Web.Mvc.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LFM.Web.Mvc.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IMentorsProvider _mentorsProvider;
        private readonly AppConfigurations _appConfigs;
        private readonly LfmDbContext _context;
        public HomeController(
            ILogger<HomeController> logger, 
            IMentorsProvider mentorsProvider,
            IOptions<AppConfigurations> configOptions, 
            LfmDbContext context)
        {
            _logger = logger;
            _mentorsProvider = mentorsProvider;
            _context = context;
            _appConfigs = configOptions.Value;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("looking-for-mentors")]
        public async Task<IActionResult> LookingForMentors([FromQuery]MentorsSearchModel searchModel, int? pageNumber)
        {
            var mentors = await _mentorsProvider.LookingForMentors(searchModel, pageNumber);

            if (mentors.TotalCount == 0)
            {
                this.AlertWarning(Messages.DataNotFound, "Викладачів");
            }
            
            CommonStaticService.PushLastSearchMentorsRequest(HttpContext, searchModel);

            MentorsListPageModel pageModel = new MentorsListPageModel(mentors, searchModel, _appConfigs.SearchingMentorsPageSize);
            
            return View("../Mentors/Mentors", pageModel);
        }

        public async Task<IActionResult> Privacy()
        {
            return View();
        }
    }
}
