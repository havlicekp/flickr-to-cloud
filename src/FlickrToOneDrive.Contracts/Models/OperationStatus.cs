namespace FlickrToOneDrive.Contracts.Models
{
    public class OperationStatus
    {
        public OperationStatus(int percentageComplete, string status, string operation, bool successResponseCode, string monitorUrl)
        {
            PercentageComplete = percentageComplete;
            Status = status;
            Operation = operation;
            SuccessResponseCode = successResponseCode;
            MonitorUrl = monitorUrl;
        }

        public int PercentageComplete { get; }
        public string Status { get; set; }
        public string Operation { get; set; }
        public bool SuccessResponseCode { get; set; }
        public string MonitorUrl { get; set; }
    }
}
