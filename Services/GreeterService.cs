using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Grpc.Core;
using VideoProto; // Import generated gRPC classes

//Consumer thread class
public class ConsumerThread
{
    //self-buffer
    private List<(int, byte[])> fileChunks;
    private string? currentFile { get; set; }
    private string? currentByteIndex { get; set; }
    private bool isRunning { get; set; }
    private bool fileCompleted { get; set; }

    public ConsumerThread()
    {
        fileChunks = new List<(int, byte[])>();
        currentFile = null;
        currentByteIndex = null;
        isRunning = true;
        fileCompleted = false;
    }

    public void runConsumer()
    {
        while (isRunning)
        {

        }
    }

}

public class VideoConsumer : VideoService.VideoServiceBase
{
    private readonly ConcurrentDictionary<string, List<(int, byte[])>> _fileChunks = new();
    private readonly List<string> assignedFiles = new List<string>();
    private readonly object _lock = new();
    static private int maxBufferSize = 100;
    private bool initialRun = true;


    public override async Task<UploadResponse> UploadVideo(IAsyncStreamReader<VideoChunk> requestStream, 
                                                           IServerStreamWriter<UploadResponse> responseStream,
                                                           ServerCallContext context)
    {
        try
        {
            if (initialRun) { 
                //initialization
                await requestStream.MoveNext(context.CancellationToken);

                var chunk = requestStream.Current;

                if(chunk.Config != null)
                {
                    //check if pThreads are higher than cThreads
                }

            }
            while (await requestStream.MoveNext(context.CancellationToken))
            {
                var chunk = requestStream.Current;
                //stop sending
                if (_fileChunks.Count >= maxBufferSize)
                {
                    //tells it that the buffer is full and that the currChunk was not stored
                    //Chunk is resent as a countermeasure for dropped data
                    await responseStream.WriteAsync(new UploadResponse { CurrStatus = UploadResponse.Types.status.Full, CurrChunk = chunk });
                }
                else
                {
                    await responseStream.WriteAsync(new UploadResponse { CurrStatus = UploadResponse.Types.status.Ok });

                    lock (_lock)
                    {
                        if (!_fileChunks.ContainsKey(chunk.Metadata.FileName))
                        {
                            _fileChunks[chunk.Metadata.FileName] = new List<(int, byte[])>();
                        }
                        _fileChunks[chunk.Metadata.FileName].Add((chunk.Metadata.ChunkIndex, chunk.Metadata.Data.ToByteArray()));

                        Console.WriteLine($"Received chunk {chunk.Metadata.ChunkIndex}/{chunk.Metadata.TotalChunks} for {chunk.Metadata.FileName}");
                    }
                }
            }
            return new UploadResponse { CurrStatus = UploadResponse.Types.status.Complete };
        }
        catch (Exception ex)
        {
            return new UploadResponse { CurrStatus = UploadResponse.Types.status.Wait };
        }
    }

    private void ProcessChunks()
    {
        lock (_lock)
        {
            foreach (var file in _fileChunks)
            {
                var fileName = file.Key;
                var sortedChunks = file.Value.OrderBy(c => c.Item1).Select(c => c.Item2).ToList();
                string outputPath = Path.Combine("UploadedVideos", fileName);

                Directory.CreateDirectory("UploadedVideos");

                using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                foreach (var chunk in sortedChunks)
                    fileStream.Write(chunk, 0, chunk.Length);

                Console.WriteLine($"File {fileName} assembled successfully.");
            }

            _fileChunks.Clear(); // Cleanup after processing
        }
    }
}