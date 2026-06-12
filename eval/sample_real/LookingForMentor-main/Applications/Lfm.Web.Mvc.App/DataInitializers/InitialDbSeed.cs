using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using LFM.DataAccess.DB.Core.Context;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Entities.ToDoEntities;
using LFM.DataAccess.DB.Core.Types;
using Lfm.Domain.Common.Services.Role;
using LFM.Domain.Write.ToDo;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lfm.Web.Mvc.App.DataInitializers
{
    public static class InitialDbSeed
    {
        public static async Task Init(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var services = scope.ServiceProvider;
            try
            {
                var lfmContext = services.GetRequiredService<LfmDbContext>();
                await InitializeToDoOperationCodes(lfmContext);
                
                var userManager = services.GetRequiredService<UserManager<LfmUser>>();
                var lfmRoleManager = services.GetRequiredService<ILfmRoleManager>();
                await InitializeUsers(userManager, lfmRoleManager);
            }
            catch (Exception ex)
            {
                var logger = services.GetRequiredService<ILogger>();
                logger.LogError(ex, "An error occurred while seeding the database.");
            }
        }

        private static async Task InitializeToDoOperationCodes(LfmDbContext context)
        {
            var operations = typeof(ToDoOperationsEnum)
                .GetFields(BindingFlags.Static | BindingFlags.Public)
                .Where(p => p.FieldType == typeof(ToDoOperationsEnum))
                .Select(p => p.GetValue(null) as ToDoOperationsEnum?)
                .ToList();

            foreach (var o in operations)
            {
                if (o.HasValue && 
                    !await context.ToDoOperationCodes
                        .AnyAsync(op => op.Id == o.Value.Id && op.Code == o.Value.Code))
                {
                    context.ToDoOperationCodes.Add(new ToDoOperation
                    {
                        Id = o.Value.Id,
                        Code = o.Value.Code,
                        Description = o.Value.Description
                    });
                }
                await context.SaveChangesAsync();
            }
        }

        private static async Task InitializeUsers(UserManager<LfmUser> userManager, ILfmRoleManager lfmRoleManager)
        {
            string defaultAdminEmail = "admin@mail.com";
            if (await userManager.FindByEmailAsync(defaultAdminEmail) == null)
            {
                LfmUser admin = new LfmUser
                {
                    Name = "Admin",
                    Email = defaultAdminEmail,
                    UserName = defaultAdminEmail
                };
                IdentityResult result = await userManager.CreateAsync(admin, "123456qwer");
                if (result.Succeeded)
                {
                    await lfmRoleManager.SetRoleToUser(admin, LfmIdentityRolesEnum.Admin);
                }
            }
            
            string defaultManagerEmail = "manager@mail.com";
            if (await userManager.FindByEmailAsync(defaultManagerEmail) == null)
            {
                LfmUser manager = new LfmUser
                {
                    Name = "Manager",
                    Email = defaultManagerEmail,
                    UserName = defaultManagerEmail
                };
                IdentityResult result = await userManager.CreateAsync(manager, "123456qwer");
                if (result.Succeeded)
                {
                    await lfmRoleManager.SetRoleToUser(manager, LfmIdentityRolesEnum.Manager);
                }
            }
            
            string defaultMentorEmail = "mentor@mail.com";
            if (await userManager.FindByEmailAsync(defaultMentorEmail) == null)
            {
                LfmUser mentor = new LfmUser
                {
                    Name = "Mentor",
                    Email = defaultMentorEmail, 
                    UserName = defaultMentorEmail
                };
                IdentityResult result = await userManager.CreateAsync(mentor, "123456qwer");
                if (result.Succeeded)
                {
                    await lfmRoleManager.SetRoleToUser(mentor, LfmIdentityRolesEnum.Mentor);
                }
            }
            
            string defaultStudentEmail = "student@mail.com";
            if (await userManager.FindByEmailAsync(defaultStudentEmail) == null)
            {
                LfmUser student = new LfmUser
                {
                    Name = "Student",
                    Email = defaultStudentEmail, 
                    UserName = defaultStudentEmail
                };
                IdentityResult result = await userManager.CreateAsync(student, "123456qwer");
                if (result.Succeeded)
                {
                    await lfmRoleManager.SetRoleToUser(student, LfmIdentityRolesEnum.Student);
                }
            }
        }
    }
}