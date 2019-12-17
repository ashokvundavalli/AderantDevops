using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;

namespace Aderant.TeamFoundation.Integration {
    internal static class StorageFactory {
        public static CloudQueue GetCloudQueue() {
            var storageAccount = CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("AzureStorageConnectionString"));
            var blobClient = storageAccount.CreateCloudQueueClient();
            var queue = blobClient.GetQueueReference("build-completed");
            return queue;
        }


    }
}