﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Foundation;
using Microsoft.Extensions.Logging;
using Shiny.Hosting;
using Shiny.Support.Repositories;

namespace Shiny.Net.Http;


public class HttpTransferManager : NSUrlSessionDownloadDelegate,
                                   IHttpTransferManager,
                                   IIosLifecycle.IHandleEventsForBackgroundUrl
{
    const string SessionName = "Shiny";
    Action? completionHandler;

    readonly ILogger logger;
    readonly IPlatform platform;
    readonly IRepository repository;
    readonly IServiceProvider services;


    public HttpTransferManager(
        AppleConfiguration config,
        ILogger<HttpTransferManager> logger,
        IRepository repository,
        IPlatform platform,
        IServiceProvider services
    )
    {
        this.logger = logger;
        this.repository = repository;
        this.platform = platform;
        this.services = services;
        this.Config = config;
    }


    public AppleConfiguration Config { get; }


    NSUrlSession? nsUrlSession;
    public NSUrlSession Session
    {
        get
        {
            if (this.nsUrlSession == null)
            {
                var config = this.Config;
                var cfg = NSUrlSessionConfiguration.CreateBackgroundSessionConfiguration(SessionName);
                if (config.HttpMaximumConnectionsPerHost != null)
                    cfg.HttpMaximumConnectionsPerHost = config.HttpMaximumConnectionsPerHost.Value;

                if (config.CachePolicy != null)
                    cfg.RequestCachePolicy = config.CachePolicy.Value;

                if (config.HttpShouldUsePipelining != null)
                    cfg.HttpShouldUsePipelining = config.HttpShouldUsePipelining.Value;

                cfg.AllowsCellularAccess = config.AllowsCellularAccess;
                cfg.AllowsConstrainedNetworkAccess = config.AllowsConstrainedNetworkAccess;
                cfg.AllowsExpensiveNetworkAccess = config.AllowsExpensiveNetworkAccess;

                cfg.SessionSendsLaunchEvents = true;
                //cfg.SharedContainerIdentifier
                //cfg.ShouldUseExtendedBackgroundIdleMode
                //cfg.WaitsForConnectivity

                this.nsUrlSession = NSUrlSession.FromConfiguration(cfg, this, new NSOperationQueue());
            }
            return this.nsUrlSession!;
        }
    } // TODO: when session completes/invalidates - we may need to be able to create new one


    public Task<IList<HttpTransfer>> GetTransfers()
    {
        var transfers = this.repository.GetList<HttpTransfer>();
        return Task.FromResult(transfers);
    }


    public async Task<HttpTransfer> Queue(HttpTransferRequest request)
    {
        request.AssertValid();
        try
        {
            var transfer = new HttpTransfer(request, 0, 0, HttpTransferState.Pending, DateTimeOffset.UtcNow);
            this.repository.Insert(transfer);

            NSUrlSessionTask task;

            if (request.IsUpload)
            {
                task = await this.CreateUpload(request).ConfigureAwait(false);
            }
            else
            {
                var nativeRequest = request.ToNative();
                task = this.Session.CreateDownloadTask(nativeRequest);
            }

            task.TaskDescription = request.Identifier;
            task.Resume();

            return transfer;
        }
        catch
        {
            this.repository.Remove<HttpTransfer>(request.Identifier);
            throw;
        }
    }


    public async Task Cancel(string identifier)
    {
        var task = (await this.Session.GetAllTasksAsync())
            .FirstOrDefault(x => x
                .TaskDescription!
                .Equals(identifier, StringComparison.InvariantCultureIgnoreCase)
            );

        if (task != null)
            task.Cancel();
    }


    public async Task CancelAll()
    {
        var tasks = await this.Session.GetAllTasksAsync();
        foreach (var task in tasks)
            task.Cancel();
    }


    readonly Subject<HttpTransferResult> transferSubj = new();
    public IObservable<HttpTransferResult> WhenUpdateReceived() => this.transferSubj;


    public bool Handle(string sessionIdentifier, Action incomingCompletionHandler)
    {
        if (SessionName.Equals(sessionIdentifier))
        {
            this.completionHandler = incomingCompletionHandler;
            return true;
        }
        return false;
    }


    public override void DidBecomeInvalid(NSUrlSession session, NSError error)
    {
        this.logger.LogDebug($"DidBecomeInvalid");
        //this.manager.CompleteSession(); // TODO: restart session?
        if (error != null)
            this.logger.LogError(new InvalidOperationException(error.LocalizedDescription), "DidBecomeInvalid reported an error");
    }


    //// this is tough to implement due to NSInputStream & CFStream delegates
    //// reauthorize?
    ////public override void NeedNewBodyStream(NSUrlSession session, NSUrlSessionTask task, Action<NSInputStream> completionHandler)
    ////{
    ////    var transfer = task.FromNative();
    ////    var file = new FileInfo(transfer.LocalFilePath);
    ////    //var stream = new BodyStream(file);
    ////    //completionHandler(stream);
    ////}


    ////public override void DidReceiveChallenge(NSUrlSession session, NSUrlAuthenticationChallenge challenge, Action<NSUrlSessionAuthChallengeDisposition, NSUrlCredential> completionHandler)
    ////{
    ////    this.logger.LogDebug($"DidReceiveChallenge");
    ////    completionHandler.Invoke(NSUrlSessionAuthChallengeDisposition.PerformDefaultHandling, null!);
    ////}


    ////public override void DidReceiveChallenge(NSUrlSession session, NSUrlSessionTask task, NSUrlAuthenticationChallenge challenge, Action<NSUrlSessionAuthChallengeDisposition, NSUrlCredential> completionHandler)
    ////{
    ////    this.logger.LogDebug($"DidReceiveChallenge for task");
    ////    completionHandler.Invoke(NSUrlSessionAuthChallengeDisposition.PerformDefaultHandling, null!);
    ////}


    //public override void DidResume(NSUrlSession session, NSUrlSessionDownloadTask downloadTask, long resumeFileOffset, long expectedTotalBytes)
    //{
    //    this.logger.LogDebug("DidResume");
    //    //this.onEvent.OnNext(downloadTask.FromNative());
    //}


    public override void DidFinishEventsForBackgroundSession(NSUrlSession session)
    {
        this.logger.LogInformation("DidFinishEventsForBackgroundSession");
        this.completionHandler?.Invoke();
    }


    public override async void DidCompleteWithError(NSUrlSession session, NSUrlSessionTask task, NSError? error)
    {
        var ht = this.repository.Get<HttpTransfer>(task.TaskDescription!);
        if (ht == null)
        {
            this.logger.NoTransferFound(task.TaskDescription!);
            return;
        }

        this.logger.LogDebug("DidCompleteWithError: " + task.State);
        switch (task.State)
        {
            case NSUrlSessionTaskState.Suspended:
                ht = ht with { Status = HttpTransferState.Paused };
                this.repository.Update(ht);

                this.transferSubj.OnNext(new HttpTransferResult(
                    ht.Request,
                    ht.Status,
                    new(0, 0, 0),
                    null
                ));
                break;

            case NSUrlSessionTaskState.Canceling:
                this.OnCancel(ht);
                break;

            case NSUrlSessionTaskState.Completed:
            default:
                var statusCode = task.GetStatusCode();

                if (task.Error != null)
                {
                    if (task.IsCancelled())
                    {
                        this.OnCancel(ht);
                    }
                    else
                    {
                        var e = task.Error;
                        var msg = $"HTTP Transfer Error - {e?.LocalizedDescription} - {e?.LocalizedFailureReason}";
                        this.OnError(ht, new InvalidOperationException(msg));
                    }
                }
                else if (statusCode < 200 || statusCode > 299)
                {
                    this.OnError(ht, new InvalidOperationException("HTTP Transfer Error - Invalid Status Code: " + statusCode));
                }
                else if (ht.Request.IsUpload)
                {
                    // If download, let DidFinishDownloading call this
                    this.logger.LogInformation($"Transfer {ht.Identifier} was completed");
                    this.OnFinish(ht);
                }
                break;
        }
    }


    void Remove(HttpTransfer ht)
    {
        this.repository.Remove(ht);
        this.TryDeleteUploadTempFile(ht);
    }


    async void OnError(HttpTransfer ht, Exception ex)
    {
        this.logger.LogError(ex, $"Transfer {ht.Identifier} was completed with error");
        this.Remove(ht);

        await this.services.RunDelegates<IHttpTransferDelegate>(x => x.OnError(ht.Request, ex), this.logger);
        this.transferSubj.OnNext(new HttpTransferResult(
            ht.Request,
            HttpTransferState.Error,
            new (0, 0, 0),
            ex
        ));
    }


    void OnCancel(HttpTransfer ht)
    {
        this.logger.LogInformation($"Transfer {ht.Identifier} was canceled");
        this.Remove(ht);

        this.transferSubj.OnNext(new HttpTransferResult(
            ht.Request,
            HttpTransferState.Canceled,
            new(0, 0, 0),
            null
        ));
    }


    async void OnFinish(HttpTransfer ht)
    {
        this.Remove(ht);
        await this.services.RunDelegates<IHttpTransferDelegate>(x => x.OnCompleted(ht.Request), this.logger);

        this.transferSubj.OnNext(new(
            ht.Request,
            HttpTransferState.Completed,
            new(0, 0, 0),
            null
        ));
    }


    public override void DidSendBodyData(NSUrlSession session, NSUrlSessionTask task, long bytesSent, long totalBytesSent, long totalBytesExpectedToSend)
    {
        var id = task.TaskDescription!;
        var ht = this.repository.Get<HttpTransfer>(id);

        if (ht == null)
        {
            this.logger.NoTransferFound(id);
        }
        else
        {
            this.logger.TransferProgress(id, totalBytesSent, totalBytesExpectedToSend);

            ht = ht with
            {
                BytesToTransfer = totalBytesExpectedToSend,
                BytesTransferred = totalBytesSent,
                Status = HttpTransferState.InProgress
            };

            try
            {
                this.repository.Update(ht);
                var bps = (int)(task.Progress?.Throughput ?? 0);
                this.transferSubj.OnNext(new HttpTransferResult(
                    ht.Request,
                    ht.Status,
                    new(bps, totalBytesExpectedToSend, totalBytesSent),
                    null
                ));
            }
            catch (RepositoryException) 
            {
                // ignore
            }
        }
    }


    public override void DidWriteData(NSUrlSession session, NSUrlSessionDownloadTask downloadTask, long bytesWritten, long totalBytesWritten, long totalBytesExpectedToWrite)
    {
        var ht = this.repository.Get<HttpTransfer>(downloadTask.TaskDescription!);

        if (ht == null)
        {
            this.logger.NoTransferFound(downloadTask.TaskDescription!);
        }
        else
        {
            this.logger.TransferProgress(downloadTask.TaskDescription!, totalBytesWritten, totalBytesExpectedToWrite);

            ht = ht with
            {
                BytesToTransfer = totalBytesExpectedToWrite,
                BytesTransferred = totalBytesWritten,
                Status = HttpTransferState.InProgress
            };

            try
            {
                this.repository.Update(ht);
                var bps = (int)(downloadTask.Progress?.Throughput ?? 0);

                this.transferSubj.OnNext(new HttpTransferResult(
                    ht.Request,
                    ht.Status,
                    new(bps, totalBytesExpectedToWrite, totalBytesWritten),
                    null
                ));
            }
            catch (RepositoryException)
            {
            }
        }
    }


    public override void DidFinishDownloading(NSUrlSession session, NSUrlSessionDownloadTask downloadTask, NSUrl location)
    {
        if (downloadTask.HasError())
            return;

        var id = downloadTask.TaskDescription!;
        var ht = this.repository.Get<HttpTransfer>(id);

        if (ht == null)
        {
            this.logger.NoTransferFound(id);
        }
        else
        {
            this.logger.StateMethod(id);
            File.Copy(location.Path!, ht.Request.LocalFilePath, true);
            this.OnFinish(ht);
        }
    }


    async Task<NSUrlSessionTask> CreateUpload(HttpTransferRequest request)
    {
        var httpMethod = request.GetHttpMethod();
        if (httpMethod != HttpMethod.Post && httpMethod != HttpMethod.Put)
            throw new ArgumentException($"Invalid Upload HTTP Verb {request.HttpMethod} - only PUT or POST are valid");

        var native = request.ToNative();
        var boundary = Guid.NewGuid().ToString("N");
        native["Content-Type"] = $"multipart/form-data; boundary=\"{boundary}\"";

        var tempPath = this.platform.GetUploadTempFilePath(request);
        this.logger.LogInformation("Writing temp form data body to " + tempPath);

        using (var fs = new FileStream(tempPath, FileMode.Create))
        { 
            if (request.HttpContent != null)
            {
                fs.WriteString("--" + boundary);
                fs.WriteString($"Content-Disposition: form-data; name=\"{request.HttpContent.ContentFormDataName ?? "value"}\"");
                fs.WriteString($"Content-Type: {request.HttpContent.ContentType}; charset={request.HttpContent.Encoding}");
                fs.WriteLine();
                fs.WriteString(request.HttpContent.Content);
                fs.WriteLine();
            }

            var fileName = Path.GetFileName(request.LocalFilePath);
            fs.WriteString("--" + boundary);

            // TODO: escape/encode filename - add utf-8 version
            fs.WriteString($"Content-Disposition: form-data; name={request.FileFormDataName}; filename={fileName}");
            fs.WriteLine();
            using (var uploadFile = File.OpenRead(request.LocalFilePath))
                await uploadFile.CopyToAsync(fs);

            fs.WriteLine();
            fs.WriteString($"--{boundary}--");
        }

        this.logger.LogInformation("Form body written");
        var tempFileUrl = NSUrl.CreateFileUrl(tempPath, null);

        var task = this.Session.CreateUploadTask(native, tempFileUrl);
        return task;
    }


    void TryDeleteUploadTempFile(HttpTransfer transfer)
    {
        if (!transfer.Request.IsUpload)
            return;

        var path = this.platform.GetUploadTempFilePath(transfer.Request);

        if (File.Exists(path))
        {
            try
            {
                this.logger.LogDebug($"Deleting temporary upload file - {transfer.Identifier}");

                // sometimes iOS will hold a file lock a bit longer than it should
                File.Delete(path);
                this.logger.LogDebug($"Temporary upload file deleted - {transfer.Identifier}");
            }
            catch (Exception ex)
            {
                this.logger.LogWarning($"Unable to delete temporary upload file - {transfer.Identifier}", ex);
            }
        }
    }
}