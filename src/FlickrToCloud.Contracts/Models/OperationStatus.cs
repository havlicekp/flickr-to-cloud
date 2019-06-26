namespace FlickrToCloud.Contracts.Models
{
    public class OperationStatus
    {
        public OperationStatus(int percentageComplete, bool successResponseCode, string rawResponse)
        {
            PercentageComplete = percentageComplete;
            SuccessResponseCode = successResponseCode;
            RawResponse = rawResponse;
        }

        public int PercentageComplete { get; }
        public bool SuccessResponseCode { get; set; }
        public string RawResponse { get; set; }
    }
}
