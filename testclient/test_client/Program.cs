using Grpc.Net.Client;
using GrpcClient;
using System;
using System.Threading.Tasks;
using Grpc.Core;

class Program {
    static async Task Main(string[] args)
    {
        // gRPC 채널을 통해 서버와 연결 설정
        var channel = GrpcChannel.ForAddress("https://localhost:50051", new GrpcChannelOptions
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
                }
            }
            catch (RpcException ex)
            {
                Console.WriteLine($"gRPC 통신 중 오류 발생: {ex.Status}");
            }
        }
    }
}