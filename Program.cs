using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;
using ProducerClass;
using VideoProto;

class Program
{
	public static async Task Main(string[] args)
	{
		// Build configuration from config.json
		var configuration = new ConfigurationBuilder()
			.SetBasePath(Directory.GetCurrentDirectory())  // Set base path to current directory
			.AddJsonFile("config.json", optional: false, reloadOnChange: true)  // Add config file
			.Build();

		// Read values from the configuration file
		var producerThreadCount = configuration.GetValue<int>("ProducerSettings:ThreadCount");
		var consumerThreadCount = configuration.GetValue<int>("ConsumerSettings:ThreadCount");
		var queueLength = configuration.GetValue<int>("ConsumerSettings:QueueLength");
		string rootFolder = "UploadedVideos"; // You can also add this to the config if needed

		Console.WriteLine($"Producer Thread Count: {producerThreadCount}");
		Console.WriteLine($"Consumer Thread Count: {consumerThreadCount}");
		Console.WriteLine($"Queue Length: {queueLength}");

		// Pass the config values to VideoProducer
		VideoProducer producer = new VideoProducer("http://192.168.50.112:5001", rootFolder, producerThreadCount, consumerThreadCount, queueLength);

		await producer.UploadFileAsync();

		Console.WriteLine("Press any key to exit...");
		Console.ReadKey();
	}
}
