using LFM.Domain.Write.CommandHandlers.Auth;
using LFM.Domain.Write.CommandHandlers.MentorProfile;
using LFM.Domain.Write.CommandHandlers.Order;
using LFM.Domain.Write.CommandHandlers.StudentProfile;
using LFM.Domain.Write.Commands.Auth;
using LFM.Domain.Write.Commands.MentorProfile;
using LFM.Domain.Write.Commands.Order;
using LFM.Domain.Write.Commands.StudentProfile;
using LFM.Domain.Write.CommandServices.Auth;
using LFM.Domain.Write.CommandValidator;
using LFM.Domain.Write.Declarations;
using LFM.Domain.Write.Mapper;
using LFM.Domain.Write.Mediator;
using LFM.Domain.Write.PrettyCommandConverter;
using LFM.Domain.Write.ResultModels;
using LFM.Domain.Write.ToDo;
using LFM.Domain.Write.ToDo.CreateService;
using LFM.Domain.Write.ToDo.Handler;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LFM.Domain.Write
{
    public static class ModuleExporter
    {
        public static void AddCommands(this IServiceCollection services)
        {
            services.AddAutoMapper(typeof(CommandToEntityMapperConfig));
            
            services.AddScoped<IMediator, Mediator.Mediator>();

            AddInternalServices(services);

            AddCommandHandlers(services);
            AddCommandValidators(services);
            AddPrettyCommandConvertors(services);
            services.AddTransient<ICreateToDoService, CreateToDoService>();
        }

        public static void AddToDoHandlers(this IServiceCollection services)
        {
            services.AddAutoMapper(typeof(CommandToEntityMapperConfig));

            services.AddScoped<INeedsApproveCommandHandler, EditMentorProfileCommandHandler>();
            services.AddScoped<INeedsApproveCommandHandler, AddMentorSubjectCommandHandler>();
            services.AddScoped<INeedsApproveCommandHandler, EditMentorSubjectCommandHandler>();
            services.AddScoped<INeedsApproveCommandHandler, CreateLookingForMentorRequestCommandHandler>();
            services.AddScoped<IToDoHandler, ToDoHandler>();

            AddCommandValidators(services);
        }

        private static void AddCommandValidators(IServiceCollection services)
        {
            services.AddTransient<ICommandValidator<EditMentorProfileCommand>, EditMentorProfileCommandHandler>();
            services.AddTransient<ICommandValidator<AddMentorSubjectCommand>, AddMentorSubjectCommandHandler>();
            services.AddTransient<ICommandValidator<EditMentorSubjectCommand>, EditMentorSubjectCommandHandler>();
            services.AddTransient<ICommandValidator<CreateLookingForMentorRequestCommand>, CreateLookingForMentorRequestCommandHandler>();
            services.AddTransient<IValidateCommandService, ValidateCommandService>();
        }
        
        private static void AddPrettyCommandConvertors(IServiceCollection services)
        {
            services.AddTransient<IPrettyCommandConverter<EditMentorProfileCommand>, EditMentorProfileCommandHandler>();
            services.AddTransient<IPrettyCommandConverter<AddMentorSubjectCommand>, AddMentorSubjectCommandHandler>();
            services.AddTransient<IPrettyCommandConverter<EditMentorSubjectCommand>, EditMentorSubjectCommandHandler>();
            services.AddTransient<IPrettyCommandConverter<CreateLookingForMentorRequestCommand>, CreateLookingForMentorRequestCommandHandler>();
            services.AddTransient<IPrettyCommandService, PrettyCommandService>();
        }

        private static void AddCommandHandlers(IServiceCollection services)
        {
            services.AddScoped<ICommandHandler<LoginUserCommand, CommandResult>, LoginUserCommandHandler>();
            services.AddScoped<ICommandHandler<LogoutUserCommand, CommandResult>, LogoutUserCommandHandler>();
            services.AddScoped<ICommandHandler<RegisterStudentCommand, CommandResult>, RegisterStudentCommandHandler>();
            services.AddScoped<ICommandHandler<RegisterMentorCommand, CommandResult>, RegisterMentorCommandHandler>();
            services.AddScoped<ICommandHandler<EditMentorProfileCommand, CommandResult>, EditMentorProfileCommandHandler>();
            services.AddScoped<ICommandHandler<AddMentorSubjectCommand, CommandResult>, AddMentorSubjectCommandHandler>();
            services.AddScoped<ICommandHandler<DeleteMentorSubjectCommand, CommandResult>, DeleteMentorSubjectCommandHandler>();
            services.AddScoped<ICommandHandler<EditMentorSubjectCommand, CommandResult>, EditMentorSubjectCommandHandler>();
            services.AddScoped<ICommandHandler<CreatePersonalOrderToMentorCommand, CreatePersonalOrderResult>, CreatePersonalOrderToMentorCommandHandler>();
            services.AddScoped<ICommandHandler<ApprovePersonalOrderCommand, IdCommandResult>, ApprovePersonalOrderCommandHandler>();
            services.AddScoped<ICommandHandler<RejectPersonalOrderCommand, CommandResult>, RejectPersonalOrderCommandHandler>();
            services.AddScoped<ICommandHandler<CreateLookingForMentorRequestCommand, CommandResult>, CreateLookingForMentorRequestCommandHandler>();
            services.AddScoped<ICommandHandler<DeleteLookingForMentorRequestCommand, CommandResult>, DeleteLookingForMentorRequestCommandHandler>();
            services.AddScoped<ICommandHandler<InterestOrderCommand, CommandResult>, InterestOrderCommandHandler>();
            services.AddScoped<ICommandHandler<ApproveMentorProposeCommand, ApproveMentorProposeResult>, ApproveMentorProposeCommandHandler>();
        }
        
        private static void AddInternalServices(IServiceCollection services)
        {
            services.AddScoped<RegistrationService>();
        }
    }
}