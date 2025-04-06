using Grpc.Net.Client;
using VideoProto;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;

namespace ProducerClass
{
	public class VideoProducer
	{
		private readonly VideoService.VideoServiceClient _client;
		private readonly int _chunkSize;
		private readonly int _threadCount;
		private readonly List<string> _inputFolders;
		private readonly int _consumerThreadCount;
		private readonly int _queueLength;
		public VideoProducer(string grpcServerAddress, string rootDirectory, int threadCount, int consumerThreadCount, int queueLength, int chunkSize = 1024 * 1024)
		{
			var channel = GrpcChannel.ForAddress(grpcServerAddress);
			_client = new VideoService.VideoServiceClient(channel);
			_threadCount = threadCount;
			_chunkSize = chunkSize;
			_consumerThreadCount = consumerThreadCount;
			_queueLength = queueLength;

			// Automatically find all subdirectories under root
			_inputFolders = Enumerable.Range(0, _threadCount)
							.Select(i => Path.Combine(rootDirectory, i.ToString()))
							.Where(Directory.Exists)
							.ToList();

			Console.WriteLine($"Found {_threadCount} producer folders:");
			foreach (var folder in _inputFolders)
			{
				Console.WriteLine($"  - {folder}");
			}
		}

		// DEFAULT method: each thread takes one folder and uploads all files in it
		public async Task UploadFileAsync()
		{
			if (_inputFolders.Count == 0)
			{
				Console.WriteLine("No producer folders found.");
				return;
			}

			using var call = _client.UploadVideo();

			var configMessage = new UploadConfig
			{
				ProducerThreadCount = _threadCount,
				ConsumerThreadCount = _consumerThreadCount,
				QueueLength = _queueLength
			};

			await call.RequestStream.WriteAsync(new VideoChunk
			{
				VidMetadata = new VideoMetadata
				{
					Data = Google.Protobuf.ByteString.CopyFromUtf8(configMessage.ToString()) // Send config as a string
				}
			});

			// Run each folder in a separate thread to process its files
			var tasks = _inputFolders.Select(async (folderPath, index) =>
			{
				var supportedExtensions = new[] { ".mp4", ".mov", ".avi", ".mkv", ".webm" };

				var files = Directory
					.EnumerateFiles(folderPath)
					.Where(file => supportedExtensions.Contains(Path.GetExtension(file).ToLower()))
					.ToList();

				// Process files in the folder
				foreach (var filePath in files)
				{
					await UploadSingleFileAsync(filePath);
				}
			});

			// Wait for all tasks to complete
			await Task.WhenAll(tasks);
		}


		// Upload one file in chunks with a per-thread queue for managing chunk uploads
		private async Task UploadSingleFileAsync(string filePath)
		{
			try
			{
				using var call = _client.UploadVideo();

				var fileName = Path.GetFileName(filePath);
				var fileBytes = File.ReadAllBytes(filePath);
				int totalChunks = (int)Math.Ceiling((double)fileBytes.Length / _chunkSize);

				// A concurrent queue for managing chunks sent by this thread
				var chunkQueue = new ConcurrentQueue<VideoChunk>();

				// Listen for consumer feedback asynchronously
				Task receivingTask = Task.Run(async () =>
				{
					await foreach (var bufferStatus in call.ResponseStream.ReadAllAsync())
					{
						Console.WriteLine($"Consumer Buffer Status: {bufferStatus.CurrStatus}");

						// Simulate acknowledgment mechanism (e.g., consumer tells which chunk has been processed)
						// For now, this could simply be a message, but you could add logic here to track which chunk is processed
					}
				});

				// Chunk and send: enqueue chunks into the queue per thread
				for (int i = 0; i < totalChunks; i++)
				{
					int offset = i * _chunkSize;
					int length = Math.Min(_chunkSize, fileBytes.Length - offset);
					var chunkData = new byte[length];
					Array.Copy(fileBytes, offset, chunkData, 0, length);

					var metadata = new VideoMetadata
					{
						Data = Google.Protobuf.ByteString.CopyFrom(chunkData),
						FileName = fileName,
						ChunkIndex = i,
						TotalChunks = totalChunks
					};

					var chunk = new VideoChunk { VidMetadata = metadata };
					chunkQueue.Enqueue(chunk);

					// Simulate a "wait" for consumer acknowledgment before sending more chunks
					if (chunkQueue.Count > 0)
					{
						// Here we just send the chunk immediately
						await call.RequestStream.WriteAsync(chunk);
						Console.WriteLine($"[Thread: {Thread.CurrentThread.ManagedThreadId}] Sent chunk {i + 1}/{totalChunks} for file {fileName}");
					}
				}

				await call.RequestStream.CompleteAsync();
				await receivingTask;
			}
			catch (Exception e)
			{
				Console.WriteLine($"Error uploading file {filePath}: {e.Message}");
			}
		}
	}
}