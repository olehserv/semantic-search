using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using AutoMapper;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using Lfm.Core.Common.Web.SessionAlerts;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Types;
using Lfm.Domain.Common.Extensions;
using LFM.Domain.Read.Providers;
using Lfm.Domain.ReadModels.ReviewModels.MentorProfile;
using LFM.Domain.Write.Commands.MentorProfile;
using LFM.Domain.Write.Mediator;
using LFM.Domain.Write.ResultModels;
using Lfm.Web.Mvc.App.Extensions;
using Lfm.Web.Mvc.Models.FormModels.UserCabinet.Mentor;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LFM.Web.Mvc.Controllers
{
    [Authorize(Roles = LfmIdentityRolesNames.Mentor)]
    [Route("user-cabinet/mentor")]
    public class MentorUserCabinetController : Controller
    {
        private readonly IMapper _mapper;
        private readonly IMentorProfileProvider _mentorProfileProvider;
        private readonly IMediator _commandBus;
        private readonly ISubjectsProvider _subjectsProvider;
        private readonly UserManager<LfmUser> _userManager;
        
        public MentorUserCabinetController(
            IMapper mapper,
            IMentorProfileProvider mentorProfileProvider,
            IMediator commandBus, 
            ISubjectsProvider subjectsProvider, 
            UserManager<LfmUser> userManager)
        {
            _mapper = mapper;
            _mentorProfileProvider = mentorProfileProvider;
            _commandBus = commandBus;
            _subjectsProvider = subjectsProvider;
            _userManager = userManager;
        }
        
        
        [HttpGet("general-info")]
        public async Task<IActionResult> GeneralInfo()
        {
            var model = await _mentorProfileProvider.GetGeneralInfo<MentorProfilePreviewModel>(User.GetId());
            return View("../UserCabinet/Mentor/GeneralInfo", model);
        }

        [HttpGet("edit-profile")]
        public async Task<IActionResult> EditGeneralInfo()
        {
            var model = await _mentorProfileProvider.GetGeneralInfo<EditMentorsProfileFormModel>(User.GetId());
            model.Name = await _userManager.GetName(User);
            return View("../UserCabinet/Mentor/EditGeneralInfo", model);
        }

        [HttpPost("edit-profile")]
        //[AlertModelStateErrors]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditGeneralInfo(EditMentorsProfileFormModel model)
        {
            var defaultResult = View("../UserCabinet/Mentor/EditGeneralInfo", model);
            return await this.HandleAction(async () => 
                { 
                    var command = _mapper.Map<EditMentorProfileCommand>(model);
                    command.MentorId = User.GetId();

                    await _commandBus.ExecuteNeedsApproveCommand<EditMentorProfileCommand, CommandResult>(command);
                    return RedirectToAction("GeneralInfo");
                },
                defaultResult);
        }

        [HttpGet("subjects-info")]
        public async Task<IActionResult> SubjectsInfo()
        {
            var subjectsInfo = await _mentorProfileProvider.GetSubjectsInfo(User.GetId());

            return View("../UserCabinet/Mentor/SubjectsInfo", subjectsInfo);
        }

        [HttpGet("adding-subject/{subjectId:int}")]
        public async Task<IActionResult> AddSubject([Required] int subjectId)
        {
            if (!await _mentorProfileProvider.CanAddSubject(User.GetId(), subjectId))
            {
                this.AlertError("Неможливо додати предмет.");
                return RedirectToAction("SubjectsInfo");
            }

            return View("../UserCabinet/Mentor/AddSubject", new AddMentorsSubjectFormModel {SubjectId = subjectId});
        }

        [HttpPost("add-subject")]
        //[AlertModelStateErrors]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddingSubject(AddMentorsSubjectFormModel model)
        {
            var defaultResult = View("../UserCabinet/Mentor/AddSubject", model);
            
            return await this.HandleAction(async () => 
                { 
                    if (!await _subjectsProvider.IsExists(model.SubjectId))
                    {
                        this.AlertError(Messages.DataNotFound, "Предмет");
                        return RedirectToAction("SubjectsInfo");
                    }
                
                    var command = _mapper.Map<AddMentorSubjectCommand>(model);
                    command.MentorId = User.GetId();
                    
                    await _commandBus.ExecuteNeedsApproveCommand<AddMentorSubjectCommand, CommandResult>(command);
                    return RedirectToAction("SubjectsInfo");
                },
                defaultResult);
        }

        [HttpGet("editing-subject/{subjectId:int}")]
        public async Task<IActionResult> EditSubject([Required] int subjectId)
        {
            var subject = await _mentorProfileProvider.GetSubject(User.GetId(), subjectId);
            if (subject == null)
            {
                this.AlertError(Messages.DataNotFound, "Предмет");
                return RedirectToAction("SubjectsInfo");
            }

            var model = _mapper.Map<EditMentorsSubjectFormModel>(subject);
            return View("../UserCabinet/Mentor/EditSubject", model);
        }

        [HttpPost("edit-subject")]
        //[AlertModelStateErrors]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditingSubject(EditMentorsSubjectFormModel model)
        {
            var defaultResult = View("../UserCabinet/Mentor/EditSubject", model);
            return await this.HandleAction(async () => 
                { 
                    var command = _mapper.Map<EditMentorSubjectCommand>(model);
                    command.MentorId = User.GetId();
                    
                    await _commandBus.ExecuteNeedsApproveCommand<EditMentorSubjectCommand, CommandResult>(command);
                    return RedirectToAction("SubjectsInfo");
                },
                defaultResult);
        }

        [HttpPost("delete-subject")]
        //[AlertModelStateErrors]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSubject([Required] int subjectId)
        {
            var defaultResult = RedirectToAction("SubjectsInfo");
            return await this.HandleAction(async () => 
                { 
                    DeleteMentorSubjectCommand command = new DeleteMentorSubjectCommand
                    {
                        MentorId = User.GetId(),
                        SubjectId = subjectId
                    };

                    var result = await _commandBus.ExecuteCommand(command);

                    if (result.IsSuccess)
                    {
                        this.AlertSuccess("Предмет видалено.");
                        return RedirectToAction("SubjectsInfo");
                    }
                    this.AlertError("Помилка при видаленні предмету.");
                    return defaultResult;
                },
                defaultResult);
        }

        [HttpGet("personal-orders")]
        public async Task<IActionResult> PersonalOrders()
        {
            var data = await _mentorProfileProvider.GetPersonalOrdersRequests(User.GetId());

            return View("../UserCabinet/Mentor/PersonalOrders", data);
        }
        
        [HttpGet("personal-order-details")]
        public async Task<IActionResult> PersonalOrderDetails(int orderId)
        {
            var data = await _mentorProfileProvider.GetPersonalOrderRequestDetails(User.GetId(), orderId);

            return View("../UserCabinet/Mentor/PersonalOrderDetails", data);
        }

        [HttpPost("approve-personal-order")]
        public async Task<IActionResult> ApprovePersonalOrderRequest(int orderId)
        {
            var defaultResult = RedirectToAction("PersonalOrderDetails", new {orderId });
            
            return await this.HandleAction(async () => 
                { 
                    ApprovePersonalOrderCommand command = new ApprovePersonalOrderCommand
                    {
                        MentorId = User.GetId(),
                        OrderRequestId = orderId
                    };
                
                    IdCommandResult commandResult = await _commandBus.ExecuteCommand<ApprovePersonalOrderCommand, IdCommandResult>(command);

                    if (commandResult.IsSuccess)
                    {
                        this.AlertSuccess("Персональна заявка підтверджена.");
                        return RedirectToAction("ApprovedOrderDetails", new { orderId = commandResult.Id });
                    }
                    this.AlertError("Помилка підтвердження персональної заявки.");
                    return defaultResult;
                },
                defaultResult);
        }
        
        [HttpPost("reject-personal-order")]
        public async Task<IActionResult> RejectPersonalOrderRequest(int orderId)
        {
            var defaultResult = RedirectToAction("PersonalOrderDetails", new {orderId });
            return await this.HandleAction(async () => 
                { 
                    RejectPersonalOrderCommand command = new RejectPersonalOrderCommand
                    {
                        MentorId = User.GetId(),
                        OrderRequestId = orderId
                    };
                
                    CommandResult commandResult = await _commandBus.ExecuteCommand(command);

                    if (commandResult.IsSuccess)
                    {
                        this.AlertSuccess("Персональну заявку відхилено.");
                        return RedirectToAction("PersonalOrders");
                    }
                    this.AlertError("Помилка видхилення персональної заявки.");
                    return defaultResult;
                },
                defaultResult);
        }
        
        [HttpGet("approved-orders")]
        public async Task<IActionResult> ApprovedOrders()
        {
            var data = await _mentorProfileProvider.GetApprovedOrders(User.GetId());

            return View("../UserCabinet/Mentor/ApprovedOrders", data);
        }
        
        [HttpGet("approved-order-details")]
        public async Task<IActionResult> ApprovedOrderDetails(int orderId)
        {
            var data = await _mentorProfileProvider.GetApprovedOrderDetails(User.GetId(), orderId);

            return View("../UserCabinet/Mentor/ApprovedOrderDetails", data);
        }
        
        [HttpGet("potential-orders")]
        public async Task<IActionResult> PotentialOrders()
        {
            IEnumerable<MentorPotentialOrderReviewModel> data = new List<MentorPotentialOrderReviewModel>();
            try
            {
                data = await _mentorProfileProvider.GetPotentialOrders(User.GetId());
            }
            catch (LfmException exc)
            {
                this.AlertWarning(exc.Message);
            }
            
            return View("../UserCabinet/Mentor/PotentialOrders", data);
        }
        
        [HttpGet("potential-order-details")]
        public async Task<IActionResult> PotentialOrderDetails(int orderId)
        {
            MentorPotentialOrderDetailsReviewModel order;
            try
            {
                order = await _mentorProfileProvider.GetPotentialOrderDetails(User.GetId(), orderId);
            }
            catch (LfmException exc)
            {
                this.AlertError(exc.Message);
                return RedirectToAction("PotentialOrders");
            }

            return View("../UserCabinet/Mentor/PotentialOrderDetails", order);
        }
        
        [HttpPost("potential-order-propose")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PotentialOrderPropose(int orderId)
        {
            var defaultResult = RedirectToAction("PotentialOrders", new {orderId });
            return await this.HandleAction(async () => 
                { 
                    InterestOrderCommand command = new InterestOrderCommand
                    {
                        MentorId = User.GetId(),
                        OrderId = orderId
                    };
                
                    CommandResult commandResult = await _commandBus.ExecuteCommand(command);

                    if (commandResult.IsSuccess)
                    {
                        this.AlertSuccess("Ваша кандидатура врахована.");
                        return RedirectToAction("PotentialOrderDetails", new { orderId });
                    }
                    this.AlertError("Помилка при висуванні кандидатури.");
                    return defaultResult;
                },
                defaultResult);
        }
    }
}