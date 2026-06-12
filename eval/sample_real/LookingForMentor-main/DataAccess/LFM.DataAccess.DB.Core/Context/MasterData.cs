using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Threading;
using LFM.Core.Common.Data;
using LFM.Core.Common.Exceptions;
using LFM.DataAccess.DB.Core.Entities;
using LFM.DataAccess.DB.Core.Entities.SubjectEntities;
using LFM.DataAccess.DB.Core.Entities.ToDoEntities;
using LFM.DataAccess.DB.Core.Resources;
using LFM.DataAccess.DB.Core.Types;
using Microsoft.EntityFrameworkCore;

namespace LFM.DataAccess.DB.Core.Context
{
    public static class MasterData
    {
        public static void Register(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LfmRole>().HasData(
                new LfmRole
                {
                    Id = (int)LfmIdentityRolesEnum.Student,
                    Name = LfmIdentityRolesNames.Student,
                    NormalizedName = LfmIdentityRolesNames.Student.ToUpper()
                },
                new LfmRole
                {
                    Id = (int)LfmIdentityRolesEnum.Mentor,
                    Name = LfmIdentityRolesNames.Mentor,
                    NormalizedName = LfmIdentityRolesNames.Mentor.ToUpper()
                }, 
                new LfmRole
                {
                    Id = (int)LfmIdentityRolesEnum.Admin,
                    Name = LfmIdentityRolesNames.Admin,
                    NormalizedName = LfmIdentityRolesNames.Admin.ToUpper()
                }, 
                new LfmRole
                {
                    Id = (int)LfmIdentityRolesEnum.Manager,
                    Name = LfmIdentityRolesNames.Manager,
                    NormalizedName = LfmIdentityRolesNames.Manager.ToUpper()
                });
            
            List<SubjectsTag> Tags = new List<SubjectsTag>
            {
                new SubjectsTag
                {
                    Id = 1,
                    Name = "Базовий рівень"
                },
                new SubjectsTag
                {
                    Id = 2,
                    Name = "Початкова школа"
                },
                new SubjectsTag
                {
                    Id = 3,
                    Name = "Середня школа"
                },
                new SubjectsTag
                {
                    Id = 4,
                    Name = "Старша школа"
                },
                new SubjectsTag
                {
                    Id = 5,
                    Name = "Для початківців"
                },
                new SubjectsTag
                {
                    Id = 6,
                    Name = "Для професіоналів"
                },
                new SubjectsTag
                {
                    Id = 7,
                    Name = "Університетські курси"
                }
            };
            
            modelBuilder.Entity<SubjectsTag>().HasData(Tags.ToArray());
            
            modelBuilder.Entity<Subject>().HasData(new[]
            {
                new Subject
                {
                    Id = 1,
                    Name = "Математика"
                },
                new Subject
                {
                    Id = 2,
                    Name = "Програмування C#"
                },
                new Subject
                {
                    Id = 3,
                    Name = "Англійська мова"
                }
            });
            
            modelBuilder.Entity<TagSubjectRelation>().HasData(new[]
            {
                new TagSubjectRelation
                {
                    SubjectId = 1,
                    TagId = 2
                },
                new TagSubjectRelation
                {
                    SubjectId = 1,
                    TagId = 3
                },
                new TagSubjectRelation
                {
                    SubjectId = 1,
                    TagId = 4
                },
                new TagSubjectRelation
                {
                    SubjectId = 2,
                    TagId = 1
                },
                new TagSubjectRelation
                {
                    SubjectId = 2,
                    TagId = 5
                },
                new TagSubjectRelation
                {
                    SubjectId = 2,
                    TagId = 6
                },
                new TagSubjectRelation
                {
                    SubjectId = 2,
                    TagId = 7
                },
                new TagSubjectRelation
                {
                    SubjectId = 3,
                    TagId = 1
                },
                new TagSubjectRelation
                {
                    SubjectId = 3,
                    TagId = 2
                },
                new TagSubjectRelation
                {
                    SubjectId = 3,
                    TagId = 3
                },
                new TagSubjectRelation
                {
                    SubjectId = 3,
                    TagId = 4
                },
                new TagSubjectRelation
                {
                    SubjectId = 3,
                    TagId = 5
                },
                new TagSubjectRelation
                {
                    SubjectId = 3,
                    TagId = 6
                },
                new TagSubjectRelation
                {
                    SubjectId = 3,
                    TagId = 7
                }
            });
            
            modelBuilder.Entity<Town>().HasData(GetTowns());
            
            modelBuilder.Entity<ToDoStatus>().HasData(
                new ToDoStatus
                {
                    Id = (int)ToDoStatusEnum.Pending,
                    StatusName = ToDoStatusEnum.Pending.ToString()
                }, 
                new ToDoStatus
                {
                    Id = (int)ToDoStatusEnum.OnReview,
                    StatusName = ToDoStatusEnum.OnReview.ToString()
                }, 
                new ToDoStatus
                {
                    Id = (int)ToDoStatusEnum.Approved,
                    StatusName = ToDoStatusEnum.Approved.ToString()
                }, 
                new ToDoStatus
                {
                    Id = (int)ToDoStatusEnum.Rejected,
                    StatusName = ToDoStatusEnum.Rejected.ToString()
                });
        }

        private static Town[] GetTowns()
        {
            ResourceSet townsResource =
                UkrainianTowns.ResourceManager.GetResourceSet(Thread.CurrentThread.CurrentCulture, true, true);
            
            if (townsResource == null)
            {
                throw new LfmException(Messages.DataNotFound);
            }

            ICollection<Town> towns = townsResource.Cast<DictionaryEntry>()
                .Select(item => new Town
                {
                    Id = int.Parse(item.Key.ToString()), 
                    Name = item.Value?.ToString()
                })
                .OrderBy(t => t.Id).ToList();

            return towns.ToArray();
        }
    }
}