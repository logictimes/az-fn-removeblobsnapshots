#r "SendGrid"
#r "Microsoft.WindowsAzure.Storage"

using System;
using SendGrid.Helpers.Mail;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

public static Mail Run(TimerInfo myTimer, TraceWriter log)
{
    var today = DateTime.Today.ToShortDateString();
   
    //create empty mail report
    Mail message = new Mail()
    {
        Subject = $"Daily Blob cleaning report for {today}"
    };

    string returnBody=$"Deleting Blob snapshots started at {DateTime.Now}"+Environment.NewLine;
    
    //get Blob details from Application Settings
    var containerName = System.Environment.GetEnvironmentVariable("storageContainer");
    var connectionString = System.Environment.GetEnvironmentVariable("storageAccount");
    int retentionDays;
    try
    {
        retentionDays = int.Parse(System.Environment.GetEnvironmentVariable("retentionDays"));
    }
    catch
    {
        //default retention policy is 30 days
        retentionDays = 30;
    }


    //validate connectionstring
    var storageAccount = CloudStorageAccount.Parse(connectionString);
    //create client
    var client = storageAccount.CreateCloudBlobClient();
    //getting Container
    var container = client.GetContainerReference(containerName);

    var Endpoint = storageAccount.BlobEndpoint;

    returnBody+=Environment.NewLine+$"Working with {Endpoint}{containerName}."+Environment.NewLine;
    returnBody+=Environment.NewLine+$"Current retention policy is {retentionDays} days."+Environment.NewLine;
    returnBody+=Environment.NewLine+$"The following Blob snapshots deleted:"+Environment.NewLine;



    var listSnapshots = container
        .ListBlobs(null, true, BlobListingDetails.Snapshots)
        .Cast<CloudBlob>()
        .Where(_ => _.IsSnapshot)
        .OrderBy(_ => _.SnapshotTime);
    foreach (var snap in listSnapshots)
    {
        var dto = snap.SnapshotTime ?? DateTime.Now.ToUniversalTime();
        if ((DateTime.Now.ToUniversalTime() - dto.ToUniversalTime()).TotalDays >= retentionDays)
        {
            var snapname = snap.Name;
            var SnapshotTime = snap.SnapshotTime;
            if (snap.DeleteIfExists())
            {
                returnBody += Environment.NewLine + snap.Name + " - "+ dto.ToLocalTime().ToString()+Environment.NewLine;
            }
        }
    }

    Content content = new Content
    {
        Type = "text/plain",
        Value = returnBody
    };

    message.AddContent(content);
    return message;
}
