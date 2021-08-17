using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;

namespace Console.BlobDownload;

public class Program {

	static async Task Main(string[] args) {

		using IHost host = CreateHostBuilder(args).Build();
		await host.RunAsync();


	}

	static IHostBuilder CreateHostBuilder(string[] args) =>
		Host.CreateDefaultBuilder(args)
			.ConfigureAppConfiguration((hostingContext, configuration) => {
				 configuration.Sources.Clear();
				 IHostEnvironment env = hostingContext.HostingEnvironment;
				 configuration
					.SetBasePath(Directory.GetCurrentDirectory())
					.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
					.AddJsonFile($"appsettings.Development.json", true, true);
				IConfigurationRoot configurationRoot = configuration.Build();
			})
			.ConfigureServices((_, services) =>
				services.AddHostedService<ExampleHostedService>());
}

public class ExampleHostedService : IHostedService {
	private BlobServiceClient _storageAccount;
	private BlobContainerClient _container;

	private readonly ILogger _logger;
	private readonly IHostApplicationLifetime _appLifetime;
	private readonly IConfiguration configuration;

	public ExampleHostedService(ILogger<ExampleHostedService> logger,IHostApplicationLifetime appLifetime, IConfiguration configuration) {
      _logger = logger;
		_appLifetime = appLifetime;
		this.configuration = configuration;
		appLifetime.ApplicationStarted.Register(OnStarted);
      appLifetime.ApplicationStopping.Register(OnStopping);
      appLifetime.ApplicationStopped.Register(OnStopped);
		_storageAccount = new BlobServiceClient(configuration["StorageAccountConnection"]);
		_container = _storageAccount.GetBlobContainerClient(configuration["BlobContainer"]);
	}

   public Task StartAsync(CancellationToken cancellationToken) {
      _logger.LogInformation("1. StartAsync has been called.");
      return Task.CompletedTask;
   }

   public Task StopAsync(CancellationToken cancellationToken) {
      _logger.LogInformation("4. StopAsync has been called.");

      return Task.CompletedTask;
   }

   private void OnStarted() {
      _logger.LogInformation("2. OnStarted has been called.");
		var list = _container.GetBlobs();
		var blobs = list.Where(b => DateTimeOffset.Now.Subtract(b.Properties.LastModified.Value).TotalDays < 1).ToList();
		System.Console.WriteLine($"{blobs.Count} files found");
		foreach (var item in blobs) {
			string name = item.Name;
			BlockBlobClient blockBlob = _container.GetBlockBlobClient(name);
			if (Path.GetExtension(item.Name).Length == 0) {
				name += ".pdf";
			}
			var filename = Path.Combine(configuration["LocalFilesDestination"], name);
			Directory.CreateDirectory(Path.GetDirectoryName(filename));
			using var fileStream = File.OpenWrite(filename);
			blockBlob.DownloadTo(fileStream);
		}
		_appLifetime.StopApplication();
	}

   private void OnStopping() {
      _logger.LogInformation("3. OnStopping has been called.");
   }

   private void OnStopped() {
      _logger.LogInformation("5. OnStopped has been called.");
   }
}