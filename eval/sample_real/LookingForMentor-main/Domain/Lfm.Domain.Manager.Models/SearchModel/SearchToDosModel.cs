namespace Lfm.Domain.Manager.Models.SearchModel
{
    public class SearchToDosModel
    {
        public int? OperationId { get; set; }

        public int? UserId { get; set; }

        public SearchToDosModel(int? operationId, int? userId)
        {
            OperationId = operationId;
            UserId = userId;
        }

        public SearchToDosModel()
        {
            
        }
    }
}