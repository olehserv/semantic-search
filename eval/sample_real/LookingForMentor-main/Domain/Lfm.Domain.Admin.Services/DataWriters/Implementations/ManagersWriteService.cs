using System;
using System.Threading.Tasks;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using LFM.DataAccess.DB.Core.Context;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Entities.Administration;
using Lfm.Domain.Admin.Models.WriteModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Lfm.Domain.Admin.Services.DataWriters.Implementations
{
    internal class ManagersWriteService : IManagersWriteService
    {
        private readonly LfmDbContext _context;
        private readonly UserManager<LfmUser> _userManager;

        public ManagersWriteService(
            LfmDbContext context, 
            UserManager<LfmUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task CreateManager(CreateManagerModel createModel)
        {
            string normalizedEmail = _userManager.NormalizeEmail(createModel.Email);
            if (await _context.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail))
                throw new LfmException(Messages.UserWithEmailAlreadyExists, createModel.Email);
            
            string creationStamp = Guid.NewGuid().ToString("N");
            
            _context.PendingManagerCreations.Add(new PendingManagerCreation
            {
                Email = createModel.Email,
                Name = createModel.Name,
                PhoneNumber = createModel.PhoneNumber,
                CreationStamp = creationStamp
            });
            await _context.SaveChangesAsync();
        }

        public async Task BlockManager(int managerId)
        {
            _context.BlockedManagers.Add(new BlockedManager {ManagerId = managerId});
            await _context.SaveChangesAsync();
        }

        public async Task UnBlockManager(int managerId)
        {
            var entity = await _context.BlockedManagers.FirstOrDefaultAsync(m => m.ManagerId == managerId);
            _context.BlockedManagers.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }
}