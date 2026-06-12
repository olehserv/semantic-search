using System.Collections.Generic;
using Lfm.Domain.ReadModels.Common;

namespace Lfm.Domain.ReadModels.ReviewModels.Subject
{
    public class SubjectReviewModel
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public List<CommonReviewModel> Tags { get; set; }
    }
}