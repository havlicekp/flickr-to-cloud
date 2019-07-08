using System;
using System.Threading.Tasks;
using Microsoft.Graph;
using Serilog;

namespace FlickrToCloud.Common
{
    public static class GraphUtils
    {
        public static void TryHandleGraphCancellation(ServiceException e, string message, ILogger log)
        {
            log.Error(e, $"Microsoft Graph exception ({message})");
            if (e.InnerException != null &&
                (e.InnerException is TaskCanceledException || e.InnerException is OperationCanceledException))
            {
                throw new OperationCanceledException();
            }
        }

    }
}
