using System;
using System.Collections.Generic;
using Lfm.Domain.Administration.Common.Models;
using LFM.Domain.Write.PrettyCommandConverter;

namespace Lfm.Domain.Manager.Models.ReviewModels
{
    public class ToDoDetailedReviewModel : BaseResponseModel
    {
        public int Id { get; set; }
        
        public string OperationCode { get; set; }
        
        public string OperationDescription { get; set; }

        public string CreatedByUser { get; set; }

        public DateTime CreatedDateTime { get; set; }

        public ICollection<CommandField> Command { get; set; }
    }
}