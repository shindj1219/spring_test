// See https://aka.ms/new-console-template for more information
using Grpc.Net.Client;
using GrpcClient;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GrpcClientService
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Windows 서비스로 실행되도록 설정
            var host = Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureServices(services =>
                {
                    services.AddHostedService<GrpcWorker>();
                })
                .Build();

            await host.RunAsync();
        }
    }

    public class GrpcWorker : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // gRPC 서버와의 연결 설정
            var channel = GrpcChannel.ForAddress("https://192.168.0.84:50051", new GrpcChannelOptions
            {
                HttpHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                }
            });

            // gRPC에서 생성된 PushServiceClient 객체 생성
            var client = new PushService.PushServiceClient(channel);

            // 요청 생성
            var request = new SubscribeRequest { ClientId = "client-csharp-1" };

            // 서버로부터 스트리밍 응답 받기
            using (var streamingCall = client.SubscribeToUpdates(request))
            {
                try
                {
                    // 스트리밍된 메시지 수신 대기
                    await foreach (var message in streamingCall.ResponseStream.ReadAllAsync())
                    {
                        Console.WriteLine($"서버로부터 받은 메시지: {message.Message}");
                        switch (message.PolicyCase)
                        {
                            case UpdateMessage.PolicyOneofCase.Clipboard:
                                Console.WriteLine($"클립보드 정책 업데이트: {message.Clipboard}");
                                break;
                            case UpdateMessage.PolicyOneofCase.UsbRedirection:
                                Console.WriteLine($"클립보드 정책 업데이트: {message.UsbRedirection}");
                                break;
                            default:
                                Console.WriteLine("No message content received.");
                                break;
                        }
                    }
                }
                catch (RpcException ex)
                {
                    Console.WriteLine($"gRPC 통신 중 오류 발생: {ex.Status}");
                }
            }
        }
    }
}