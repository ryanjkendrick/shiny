﻿using Shiny.Net.Http;
using Shiny.Notifications;

namespace Sample.HttpTransfers;


public partial class MyHttpTransferDelegate : IHttpTransferDelegate
{
    readonly INotificationManager notificationManager;
    readonly SampleSqliteConnection conn;


    public MyHttpTransferDelegate(INotificationManager notificationManager, SampleSqliteConnection conn)
    {
        this.notificationManager = notificationManager;
        this.conn = conn;
    }


    public Task OnError(HttpTransferRequest request, Exception ex)
        => this.CreateHttpTransferEvent(request, ex);


    public Task OnCompleted(HttpTransferRequest request)
        => this.CreateHttpTransferEvent(request);


    async Task CreateHttpTransferEvent(HttpTransferRequest request, Exception? exception = null)
    {
        var state = exception == null ? $"Completed" : "Failed";
        var direction = request.IsUpload ? "Upload" : "Download";
        var msg = $"{direction} of {Path.GetFileName(request.LocalFilePath)} {state}";

        await this.conn.Log("HTTP Transfer", msg, exception?.ToString());
        await this.notificationManager.Send("HTTP Transfer", msg);
    }
}

#if ANDROID
public partial class MyHttpTransferDelegate : IAndroidHttpTransferDelegate
{
    public void ConfigureNotification(AndroidX.Core.App.NotificationCompat.Builder builder, HttpTransferRequest request)
    {
        if (request.IsUpload)
        {
            builder.SetContentText($"Uploading {Path.GetFileName(request.LocalFilePath)}");
        }
        else
        {
            builder.SetContentText($"Downloading file from {request.Uri}");
        }
    }
}
#endif