using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using AutoMapper;
using LFM.Core.Common.Data;
using Lfm.Core.Common.Web.SessionAlerts;
using LFM.DataAccess.DB.Core.Types;
using Lfm.Domain.Common.Extensions;
using LFM.Domain.Read.Providers;
using LFM.Domain.Write.Commands.StudentProfile;
using LFM.Domain.Write.Mediator;
using LFM.Domain.Write.ResultModels;
using Lfm.Web.Mvc.App.Extensions;
using Lfm.Web.Mvc.Models.FormModels.UserCabinet.Student;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LFM.Web.Mvc.Controllers
{
    [Authorize(Roles = LfmIdentityRolesNames.Student)]
    [Route("user-cabinet/student")]
    public class StudentUserCabinetController : Controller
    {
        private readonly IMapper _mapper;
        private readonly IStudentProfileProvider _studentProfileProvider;
        private readonly ISubjectsProvider _subjectsProvider;
        private readonly IMediator _commandBus;
        
        public StudentUserCabinetController(
            IMapper mapper,
            IStudentProfileProvider studentProfileProvider,
            IMediator commandBus, 
            ISubjectsProvider subjectsProvider)
        {
            _mapper = mapper;
            _studentProfileProvider = studentProfileProvider;
            _commandBus = commandBus;
            _subjectsProvider = subjectsProvider;
        }
        
        [HttpGet("lfm-requests")]
        public async Task<IActionResult> LfmRequests()
        {
            var requests = await _studentProfileProvider.GetLfmRequests(User.GetId());
            
            return View("../UserCabinet/Student/LfmRequests", requests);
        }
        
        [HttpGet("lfm-request-details")]
        public async Task<IActionResult> LfmRequestDetails(int orderId)
        {
            var request = await _studentProfileProvider.GetLfmRequestDetails(User.GetId(), orderId);
            
            return View("../UserCabinet/Student/LfmRequestDetails", request);
        }

        [HttpGet("create-lfm-request")]
        public async Task<IActionResult> CreateOrderRequest(int subjectId)
        {
            if (!await _subjectsProvider.IsExists(subjectId))
            {
                this.AlertError(Messages.DataNotFound, "Предмет");
                return RedirectToAction("LfmRequests");
            }

            CreateLookingForMentorRequestFormModel model = new CreateLookingForMentorRequestFormModel
            {
                SubjectId = subjectId
            };
            return View("../UserCabinet/Student/CreateOrderRequest", model);
        }
        
        [HttpPost("create-lfm-request")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOrderRequest(CreateLookingForMentorRequestFormModel model)
        {
            var defaultResult = RedirectToAction("LfmRequests");
            return await this.HandleAction(async () => 
                { 
                    if (!await _subjectsProvider.IsExists(model.SubjectId))
                    {
                        this.AlertError(Messages.DataNotFound, "Предмет");
                        return RedirectToAction("LfmRequests");
                    }
                    
                    var command = _mapper.Map<CreateLookingForMentorRequestCommand>(model);
                    command.StudentId = User.GetId();
                    command.StudentName = User.GetName();
                    command.StudentEmail = User.GetEmail();
                    command.StudentPhoneNumber = User.GetPhoneNumber();

                    await _commandBus.ExecuteNeedsApproveCommand<CreateLookingForMentorRequestCommand, CommandResult>(command);
                    return defaultResult;
                },
                defaultResult);
        }

        [HttpPost("delete-lfm-request")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteOrderRequest([Range(1, int.MaxValue)]int orderId)
        {
            var defaultResult = RedirectToAction("LfmRequests");
            return await this.HandleAction(async () => 
                { 
                    DeleteLookingForMentorRequestCommand command = new DeleteLookingForMentorRequestCommand
                    {
                        StudentId = User.GetId(),
                        OrderId = orderId
                    };

                    var result = await _commandBus.ExecuteCommand(command);

                    if (result.IsSuccess)
                    {
                        this.AlertSuccess("Заявка видалена.");
                        return RedirectToAction("LfmRequests");
                    }
                    this.AlertError("Помилка при видалянні заявки.");
                    return defaultResult;
                },
                defaultResult);
        }

        [HttpPost("approve-mentor-propose")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveMentorPropose(ApproveMentorProposeFormModel model)
        {
            var defaultResult = RedirectToAction("LfmRequests");
            return await this.HandleAction(async () => 
                { 
                    ApproveMentorProposeCommand command = new ApproveMentorProposeCommand
                    {
                        StudentId = User.GetId(),
                        OrderId = model.OrderId,
                        MentorId = model.MentorId
                    };

                    var result = await _commandBus.ExecuteCommand<ApproveMentorProposeCommand, ApproveMentorProposeResult>(command);

                    if (result.IsSuccess)
                    {
                        this.AlertSuccess($"Підтверджено. Тепер ви можете зв'язатись з {result.MentorName}.");
                        return RedirectToAction("ApprovedOrderDetails", new { orderId = result.Id });
                    }
                    this.AlertError("Помилка підтвердження кандидатури.");
                    return defaultResult;
                },
                defaultResult);
        }
        
        [HttpGet("personal-requests-to-mentors")]
        public async Task<IActionResult> PersonalRequestsToMentors()
        {
            var requests = await _studentProfileProvider.GetPersonalRequestsToMentors(User.GetId());
            
            return View("../UserCabinet/Student/PersonalRequestsToMentors", requests);
        }
        
        [HttpGet("personal-request-to-mentor-details")]
        public async Task<IActionResult> PersonalRequestsToMentorDetails(int orderId)
        {
            var request = await _studentProfileProvider.GetPersonalRequestToMentorDetails(User.GetId(), orderId);
            
            return View("../UserCabinet/Student/PersonalRequestsToMentorDetails", request);
        }
        
        [HttpGet("approved-orders")]
        public async Task<IActionResult> ApprovedOrders()
        {
            var requests = await _studentProfileProvider.GetApprovedRequests(User.GetId());
            
            return View("../UserCabinet/Student/ApprovedOrders", requests);
        }
        
        [HttpGet("approved-order-details")]
        public async Task<IActionResult> ApprovedOrderDetails(int orderId)
        {
            var request = await _studentProfileProvider.GetApprovedRequestDetails(User.GetId(), orderId);
            
            return View("../UserCabinet/Student/ApprovedOrderDetails", request);
        }
    }
}