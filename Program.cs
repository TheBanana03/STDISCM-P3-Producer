using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProducerClass;
using VideoProto;

class Program
{
	public static async Task Main(string[] args)
	{
		int threadCount = 2;
		string rootFolder = "UploadedVideos";

		VideoProducer producer = new VideoProducer("http://localhost:5001", rootFolder, threadCount);

		await producer.UploadFileAsync();

		Console.WriteLine("Press any key to exit...");
		Console.ReadKey();
	}
}
