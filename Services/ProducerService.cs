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
		private readonly SemaphoreSlim _writeSemaphore = new SemaphoreSlim(1, 1); // Semaphore for serialized writes to RequestStream

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

		// Upload files with a serialized write to the RequestStream
		public async Task UploadFileAsync()
		{
			if (_inputFolders.Count == 0)
			{
				Console.WriteLine("No producer folders found.");
				return;
			}

			using var call = _client.UploadVideo();

			// Send the initial config message before starting to send video chunks
			await call.RequestStream.WriteAsync(new VideoChunk
			{
				Config = new ConfigContainer
				{
					PThreads = _threadCount,
					CThreads = _consumerThreadCount,
					QueueSize = _queueLength
				}
			});

			// Handshake with the consumer to ensure it's ready to process chunks
			if (await call.ResponseStream.MoveNext())
			{
				var response = call.ResponseStream.Current;
				if (response.CurrStatus != UploadResponse.Types.status.Init)
				{
					Console.WriteLine("Handshake failed!");
					return;
				}
				Console.WriteLine("Handshake succeeded, starting upload...");
			}

			// Process folders concurrently, but serialize writes to the RequestStream
			await Parallel.ForEachAsync(_inputFolders, new ParallelOptions { MaxDegreeOfParallelism = _threadCount },
			async (folderPath, ct) =>
			{
				var supportedExtensions = new[] { ".mp4", ".mov", ".avi", ".mkv", ".webm" };

				var files = Directory
					.EnumerateFiles(folderPath)
					.Where(file => supportedExtensions.Contains(Path.GetExtension(file).ToLower()))
					.ToList();

				foreach (var filePath in files)
				{
					await UploadSingleFileAsync(filePath, call);
				}
			});

			// Complete the RequestStream after all uploads
			await call.RequestStream.CompleteAsync();
		}

		// Upload one file in chunks with serialized writes
		private async Task UploadSingleFileAsync(string filePath, AsyncDuplexStreamingCall<VideoChunk, UploadResponse> call)
		{
			try
			{
				var fileName = Path.GetFileName(filePath);
				var fileBytes = File.ReadAllBytes(filePath);
				int totalChunks = (int)Math.Ceiling((double)fileBytes.Length / _chunkSize);

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

					await _writeSemaphore.WaitAsync();

					try
					{
						await call.RequestStream.WriteAsync(chunk);
						Console.WriteLine($"[Thread: {Thread.CurrentThread.ManagedThreadId}] Sent chunk {i + 1}/{totalChunks} for file {fileName}");
					}
					finally
					{
						_writeSemaphore.Release();
					}
				}
			}
			catch (Exception e)
			{
				Console.WriteLine($"Error uploading file {filePath}: {e.GetType().Name} - {e.Message}");
			}
		}
	}
}
