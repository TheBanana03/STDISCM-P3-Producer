using Grpc.Core;
using GRPC_Test2;

namespace GRPC_Test2.Services
{
    public class GreeterService : Greeter.GreeterBase
    {
        private readonly ILogger<GreeterService> _logger;
        public GreeterService(ILogger<GreeterService> logger)
        {
            _logger = logger;
        }

        public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        {
            return Task.FromResult(new HelloReply
            {
                Message = "Hello " + request.Name
            });
        }

        public override async Task<UploadVideoResponse> UploadFileStream(IAsyncStreamReader<UploadVideoRequest> request, ServerCallContext context)
        {
            try
            {
                var dir = "Files";
                var fileName = "temp";

                await using (var fs = System.IO.File.OpenWrite($"{dir}\\temp"))
                {
                    await foreach (var chunkMsg in request.ReadAllAsync().ConfigureAwait(false))
                    {
                        fileName = chunkMsg.Info;

                        fs.Write(chunkMsg.Data.ToByteArray());
                    }
                }

                System.IO.File.Move($"{dir}\\temp", $"{dir}\\{fileName}", true);

                _logger.LogDebug(@"[FileUploaded] 'Files\{FileName}' uploaded", fileName);
                return new UploadFileResponse() { FilePath = $@"Files\{fileName}" };
            }
            catch (Exception e)
            {
                return new UploadFileResponse() { ErrorMessage = e.Message };
            }
        }
    }
}
