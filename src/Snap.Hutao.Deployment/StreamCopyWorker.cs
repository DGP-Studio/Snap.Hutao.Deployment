using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace Snap.Hutao.Deployment;

internal delegate TStatus StreamCopyStatusFactory<out TStatus>(long bytesReadSinceLastReport, long bytesReadSinceCopyStart);

internal partial class StreamCopyWorker<TStatus> : IDisposable
{
    private readonly Stream source;
    private readonly Stream destination;
    private readonly int bufferSize;
    private readonly StreamCopyStatusFactory<TStatus> statusFactory;
    private readonly TokenBucketRateLimiter progressReportRateLimiter;

    public StreamCopyWorker(Stream source, Stream destination, StreamCopyStatusFactory<TStatus> statusFactory, int bufferSize = 81920)
    {
        this.source = source;
        this.destination = destination;
        this.statusFactory = statusFactory;
        this.bufferSize = bufferSize;

        progressReportRateLimiter = new(new()
        {
            ReplenishmentPeriod = new TimeSpan(0, 0, 0, 0, 1000),
            TokensPerPeriod = 1,
            AutoReplenishment = true,
            TokenLimit = 1,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        });
    }

    public async ValueTask CopyAsync(IProgress<TStatus> progress, CancellationToken token = default)
    {
        long bytesReadSinceCopyStart = 0;
        long bytesReadSinceLastReport = 0;

        int bytesRead;

        using (IMemoryOwner<byte> memoryOwner = MemoryPool<byte>.Shared.Rent(bufferSize))
        {
            Memory<byte> buffer = memoryOwner.Memory;

            do
            {
                bytesRead = await source.ReadAsync(buffer, token).ConfigureAwait(false);
                if (bytesRead is 0)
                {
                    progress.Report(statusFactory(bytesReadSinceLastReport, bytesReadSinceCopyStart));
                    break;
                }

                await destination.WriteAsync(buffer[..bytesRead], token).ConfigureAwait(false);

                bytesReadSinceCopyStart += bytesRead;
                bytesReadSinceLastReport += bytesRead;

                if (progressReportRateLimiter.AttemptAcquire().IsAcquired)
                {
                    progress.Report(statusFactory(bytesReadSinceLastReport, bytesReadSinceCopyStart));
                    bytesReadSinceLastReport = 0;
                }
            }
            while (bytesRead > 0);
        }
    }

    public void Dispose()
    {
        progressReportRateLimiter.Dispose();
    }
}