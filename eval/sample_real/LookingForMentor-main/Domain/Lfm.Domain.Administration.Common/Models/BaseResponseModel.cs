namespace Lfm.Domain.Administration.Common.Models
{
    public class BaseResponseModel
    {
        public bool IsSuccess { get; set; }

        public static readonly BaseResponseModel Success = new() {IsSuccess = false};
        public static readonly BaseResponseModel Failure = new() {IsSuccess = false};
    }
}