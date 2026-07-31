using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Services;
using Xunit;

namespace neo_bpsys_wpf.Tests.Services;

public sealed class FileDownloadServiceTest
{
    [Fact]
    public async Task PauseAndResume_UsesRangeAndCompletesOriginalTask()
    {
        var payload = Enumerable.Range(0, 1024 * 1024).Select(index => (byte)(index % 251)).ToArray();
        var handler = new RangeDownloadHandler(payload, delayPerRead: TimeSpan.FromMilliseconds(10));
        var service = new FileDownloadService(
            () => new HttpClient(handler, disposeHandler: false));
        var destination = CreateTemporaryDestination();
        var operation = service.CreateDownload(new FileDownloadRequest(
            new Uri("https://downloads.example.test/file.bin"),
            destination));
        var progressReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        operation.StateChanged += (_, _) =>
        {
            if (operation.State == FileDownloadState.Downloading
                && operation.Progress.BytesReceived >= 64 * 1024
                && !progressReached.Task.IsCompleted)
                progressReached.TrySetResult();
        };

        try
        {
            var running = operation.StartAsync();
            await progressReached.Task.WaitAsync(TimeSpan.FromSeconds(5));
            operation.Pause();
            Assert.Equal(FileDownloadState.Paused, operation.State);
            Assert.False(running.IsCompleted);

            operation.Resume();
            await running.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(FileDownloadState.Completed, operation.State);
            Assert.Equal(payload, await File.ReadAllBytesAsync(destination));
            Assert.Contains(handler.RequestedRanges, start => start > 0);
        }
        finally
        {
            DeleteDownloadArtifacts(destination);
        }
    }

    [Fact]
    public async Task CanceledDownload_NewOperationResumesRetainedPartialFile()
    {
        var payload = Enumerable.Range(0, 1024 * 1024).Select(index => (byte)(index % 239)).ToArray();
        var handler = new RangeDownloadHandler(payload, delayPerRead: TimeSpan.FromMilliseconds(10));
        var service = new FileDownloadService(
            () => new HttpClient(handler, disposeHandler: false));
        var destination = CreateTemporaryDestination();
        var first = service.CreateDownload(new FileDownloadRequest(
            new Uri("https://downloads.example.test/file.bin"),
            destination));
        var canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        first.StateChanged += (_, _) =>
        {
            if (first.Progress.BytesReceived >= 64 * 1024 && !canceled.Task.IsCompleted)
            {
                first.Cancel();
                canceled.TrySetResult();
            }
        };

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await first.StartAsync().WaitAsync(TimeSpan.FromSeconds(5)));
            await canceled.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(File.Exists(destination + ".download.part"));

            var second = service.CreateDownload(new FileDownloadRequest(
                new Uri("https://downloads.example.test/file.bin"),
                destination));
            await second.StartAsync().WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(payload, await File.ReadAllBytesAsync(destination));
            Assert.Contains(handler.RequestedRanges, start => start > 0);
            Assert.False(File.Exists(destination + ".download.part"));
            Assert.False(File.Exists(destination + ".download.json"));
        }
        finally
        {
            DeleteDownloadArtifacts(destination);
        }
    }

    [Fact]
    public async Task Resume_WhenServerIgnoresRange_RestartsWithoutDuplicatingBytes()
    {
        var payload = Enumerable.Range(0, 1024 * 1024).Select(index => (byte)(index % 227)).ToArray();
        var handler = new RangeDownloadHandler(payload, delayPerRead: TimeSpan.FromMilliseconds(10));
        var service = new FileDownloadService(
            () => new HttpClient(handler, disposeHandler: false));
        var destination = CreateTemporaryDestination();
        var first = service.CreateDownload(new FileDownloadRequest(
            new Uri("https://downloads.example.test/file.bin"),
            destination));
        var cancellationThresholdReached =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        first.StateChanged += (_, _) =>
        {
            if (first.Progress.BytesReceived >= 64 * 1024)
                cancellationThresholdReached.TrySetResult();
        };

        try
        {
            var firstTask = first.StartAsync();
            await cancellationThresholdReached.Task.WaitAsync(TimeSpan.FromSeconds(5));
            first.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstTask);
            Assert.True(File.Exists(destination + ".download.part"));
            handler.RequestedRanges.Clear();
            handler.IgnoreRanges = true;

            var second = service.CreateDownload(new FileDownloadRequest(
                new Uri("https://downloads.example.test/file.bin"),
                destination));
            var secondTask = second.StartAsync();
            try
            {
                await secondTask.WaitAsync(TimeSpan.FromSeconds(30));
            }
            catch (TimeoutException)
            {
                second.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => secondTask.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.Fail(
                    $"Downloader did not restart after range support disappeared. " +
                    $"State={second.State}, bytes={second.Progress.BytesReceived}, " +
                    $"resumed={second.Progress.IsResumed}, " +
                    $"ranges={string.Join(',', handler.RequestedRanges)}");
            }

            Assert.DoesNotContain(handler.RequestedRanges, start => start > 0);
            Assert.Equal(payload, await File.ReadAllBytesAsync(destination));
        }
        finally
        {
            DeleteDownloadArtifacts(destination);
        }
    }

    [Fact]
    public async Task DownloadWithoutContentLength_ReportsReceivedBytesWhileTotalIsUnknown()
    {
        var payload = Enumerable.Range(0, 512 * 1024).Select(index => (byte)(index % 211)).ToArray();
        var handler = new RangeDownloadHandler(payload, delayPerRead: TimeSpan.FromMilliseconds(10))
        {
            IgnoreRanges = true,
            OmitContentLength = true
        };
        var service = new FileDownloadService(
            () => new HttpClient(handler, disposeHandler: false));
        var destination = CreateTemporaryDestination();
        var operation = service.CreateDownload(new FileDownloadRequest(
            new Uri("https://downloads.example.test/chunked.bin"),
            destination));
        var reportedUnknownProgress = false;
        operation.StateChanged += (_, _) =>
        {
            if (operation.Progress.BytesReceived > 0 && operation.Progress.TotalBytes is null)
                reportedUnknownProgress = true;
        };

        try
        {
            await operation.StartAsync().WaitAsync(TimeSpan.FromSeconds(10));

            Assert.True(reportedUnknownProgress);
            Assert.Equal(payload, await File.ReadAllBytesAsync(destination));
        }
        finally
        {
            DeleteDownloadArtifacts(destination);
        }
    }

    private static string CreateTemporaryDestination()
    {
        var directory = Path.Combine(Path.GetTempPath(), "neo-bpsys-wpf-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "payload.bin");
    }

    private static void DeleteDownloadArtifacts(string destination)
    {
        var directory = Path.GetDirectoryName(destination);
        if (directory is null)
            return;

        for (var attempt = 0; attempt < 20 && Directory.Exists(directory); attempt++)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
        }

        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    private sealed class RangeDownloadHandler(byte[] payload, TimeSpan delayPerRead) : HttpMessageHandler
    {
        public ConcurrentQueue<long> RequestedRanges { get; } = [];

        public bool IgnoreRanges { get; set; }

        public bool OmitContentLength { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var requestedRange = request.Headers.Range?.Ranges.SingleOrDefault();
            var requestedStart = requestedRange?.From ?? 0;
            RequestedRanges.Enqueue(requestedStart);
            if (requestedStart >= payload.LongLength)
            {
                var unsatisfied = new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable)
                {
                    Content = new ByteArrayContent([])
                };
                unsatisfied.Content.Headers.ContentRange = new ContentRangeHeaderValue(payload.LongLength);
                return Task.FromResult(unsatisfied);
            }

            var rangeAccepted = requestedRange is not null && !IgnoreRanges;
            var start = rangeAccepted ? requestedStart : 0;
            var requestedEnd = requestedRange?.To ?? payload.LongLength - 1;
            var end = rangeAccepted
                ? Math.Min(requestedEnd, payload.LongLength - 1)
                : payload.LongLength - 1;
            var content = payload.AsMemory((int)start, checked((int)(end - start + 1)));
            var response = new HttpResponseMessage(
                rangeAccepted ? HttpStatusCode.PartialContent : HttpStatusCode.OK)
            {
                Content = new StreamContent(new SlowReadStream(content, delayPerRead))
            };
            response.Headers.AcceptRanges.Add(IgnoreRanges ? "none" : "bytes");
            if (!OmitContentLength)
                response.Content.Headers.ContentLength = content.Length;
            response.Headers.ETag = new EntityTagHeaderValue("\"test-etag\"");
            if (rangeAccepted)
                response.Content.Headers.ContentRange =
                    new ContentRangeHeaderValue(start, end, payload.LongLength);
            return Task.FromResult(response);
        }
    }

    private sealed class SlowReadStream(ReadOnlyMemory<byte> data, TimeSpan delayPerRead) : Stream
    {
        private int _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => data.Length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_position >= data.Length)
                return 0;
            await Task.Delay(delayPerRead, cancellationToken);
            var count = Math.Min(buffer.Length, Math.Min(16 * 1024, data.Length - _position));
            data.Slice(_position, count).CopyTo(buffer);
            _position += count;
            return count;
        }
    }
}
