using System.ComponentModel.DataAnnotations;

namespace Lfm.Web.Mvc.Models.FormModels.UserCabinet.Student
{
    public class ApproveMentorProposeFormModel
    {
        [Range(1, int.MaxValue)]
        public int OrderId { get; set; }

        [Range(1, int.MaxValue)]
        public int MentorId { get; set; }
    }
}