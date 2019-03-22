using System.Net;

namespace FlickrToOneDrive.Contracts.Models
{
    public class OperationStatus
    {
        public OperationStatus(int percentageComplete, string status, string operation, HttpStatusCode responseCode, string monitorUrl)
        {
            PercentageComplete = percentageComplete;
            Status = status;
            Operation = operation;
            ResponseCode = responseCode;
            MonitorUrl = monitorUrl;
        }

        public int PercentageComplete { get; }
        public string Status { get; set; }
        public string Operation { get; set; }
        public HttpStatusCode ResponseCode { get; set; }
        public string MonitorUrl { get; set; }
    }
}
