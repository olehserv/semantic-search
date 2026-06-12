using System.Threading.Tasks;
using AutoMapper;
using LFM.Core.Common.Data;
using Lfm.Core.Common.Web.SessionAlerts;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Types;
using Lfm.Domain.Common.Extensions;
using LFM.Domain.Read.Providers;
using Lfm.Domain.ReadModels.ReviewModels.Mentor;
using LFM.Domain.Write.Commands.Order;
using LFM.Domain.Write.Mediator;
using LFM.Domain.Write.ResultModels;
using Lfm.Web.Mvc.App.Extensions;
using Lfm.Web.Mvc.Models.FormModels.Mentor;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LFM.Web.Mvc.Controllers
{
    public class MentorsController : Controller
    {
        private readonly IMentorsProvider _mentorsProvider;
        private readonly SignInManager<LfmUser> _signInManager;
        private readonly IMapper _mapper;
        private readonly IMediator _commandBus;

        public MentorsController(
            IMentorsProvider mentorsProvider, 
            SignInManager<LfmUser> signInManager, 
            IMapper mapper, 
            IMediator commandBus)
        {
            _mentorsProvider = mentorsProvider;
            _signInManager = signInManager;
            _mapper = mapper;
            _commandBus = commandBus;
        }
        
        [HttpGet("mentor-details")]
        public async Task<IActionResult> MentorDetails(int mentorId)
        {
            var mentor = await _mentorsProvider.GetMentorInfo(mentorId);
            
            return View(mentor);
        }
        
        [HttpGet("contact-with-mentor")]
        public async Task<IActionResult> ContactWithMentor(int mentorId, int subjectId)
        {
            ContactMentorInfo contactMentorInfo = await _mentorsProvider.GetContactMentorInfo(mentorId, subjectId);
            ViewBag.ContactMentorInfo = contactMentorInfo;

            ContactMentorFormModel.UserContactInfo userContactInfo = new ContactMentorFormModel.UserContactInfo();
            if (_signInManager.IsSignedIn(HttpContext.User) && HttpContext.User.GetRole() == LfmIdentityRolesEnum.Student) 
            {
                userContactInfo.Name = HttpContext.User.GetName();
                userContactInfo.Email = HttpContext.User.GetEmail();
                userContactInfo.PhoneNumber = HttpContext.User.GetPhoneNumber();
            }

            ContactMentorFormModel model = new ContactMentorFormModel
            {
                MentorId = mentorId,
                Lesson = new ContactMentorFormModel.LessonInfo
                {
                    SubjectId = subjectId
                },
                UserContact = userContactInfo,
                Additional = new ContactMentorFormModel.AdditionalInfo()
            };

            return View(model);
        }
        
        [HttpPost("contact-with-mentor")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ContactWithMentor(ContactMentorFormModel model)
        {
            var defaultReturnResult = RedirectToAction("ContactWithMentor",
                new {mentorId = model.MentorId, subjectId = model.Lesson.SubjectId});
            
            return await this.HandleAction(async () =>
                {
                    var command = _mapper.Map<ContactMentorFormModel, CreatePersonalOrderToMentorCommand>(model);

                    if (_signInManager.IsSignedIn(HttpContext.User) &&
                        HttpContext.User.IsStudent())
                    {
                        command.StudentId = HttpContext.User.GetId();
                    }

                    var commandResult =
                        await _commandBus.ExecuteCommand<CreatePersonalOrderToMentorCommand, CreatePersonalOrderResult>(
                            command);

                    if (commandResult.IsSuccess)
                    {
                        this.AlertSuccess(Messages.PersonalOrderSuccessful, commandResult.MentorName);
                        return RedirectToAction("Index", "Home");
                    }

                    this.AlertError(Messages.PersonalOrderFailed);
                    return defaultReturnResult;
                },
                defaultReturnResult);
        }
    }
}