using System;

namespace Lfm.Domain.Manager.Models.ReviewModels
{
    public class RejectedToDoReviewModel
    {
        public int Id { get; set; }
        
        public string OperationCode { get; set; }
        

        public string CreatedByUser { get; set; }

        public DateTime CreatedDateTime { get; set; }
        
        public DateTime RejectedDateTime { get; set; }

        public string RejectedByAdmin { get; set; }
        
        public string RejectedReason { get; set; }
    }
}