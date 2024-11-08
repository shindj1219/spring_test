// See https://aka.ms/new-console-template for more information
using Grpc.Net.Client;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Management.Automation;
using System.Collections.ObjectModel;
using GrpcClient;
using System.Security;


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
        private AgentService.AgentServiceClient _client;
        private string _agentKey;
        private string _hostName;

        public GrpcWorker() {
            string configFilePath = "config.json";
            try
            {
                _hostName = System.Net.Dns.GetHostName();
                Console.WriteLine("Hostname : " + _hostName);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught. message : " + ex.Message);
            }

            if (File.Exists(configFilePath))
            {
                string jsonData = File.ReadAllText(configFilePath);
                Config? config = JsonSerializer.Deserialize<Config>(jsonData);
                if (config != null && config.Grpc != null)
                {
                    Console.WriteLine("Grpc server address : " + config.Grpc.ServerAddress);
                    // gRPC 서버와의 연결 설정
                    var channel = GrpcChannel.ForAddress(config.Grpc.ServerAddress, new GrpcChannelOptions
                    {
                        HttpHandler = new HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                        }
                    });

                    _client = new AgentService.AgentServiceClient(channel);
                }
            }
            else {
                Console.WriteLine($"File load 중 오류 발생.");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 요청 생성
            var request = new AgentInfoMessage { VdId = _hostName };
            var response = await _client.RegisterAgentAsync(request);

            if (response.AgentKey != null)
            {
                _agentKey = response.AgentKey;
            }
            else {
                Console.WriteLine("AgentKey is null.");
            }

            if (response.AdDomain != null && response.AdServerIp != null && response.AdUserId != null && response.AdUserPassword != null && response.AdOuName != null)
            {
                await ProcessADJoining(response.AdDomain, response.AdServerIp, response.AdUserId, response.AdUserPassword, response.AdOuName);
            }
            else 
            {
                await AwaitPolicy();
            }
        }

        private async Task ProcessADJoining(string adDomain, string adServerIp, string adUserId, string adUserPassword, string adOuName)
        {
            PowerShell.Create().AddCommand("Set-DnsClientServerAddress")
                               .AddParameter("InterfaceIndex", 12)
                               .AddParameter("ServerAddresses", adServerIp)
                               .Invoke();
            SecureString secureString = new SecureString();
            foreach (char c in adUserPassword) {
                secureString.AppendChar(c);
            }
            var psCredentialObject = new System.Management.Automation.PSCredential(adUserId, secureString);
            PowerShell.Create().AddCommand("Add-Computer")
                               .AddParameter("DomainName", adDomain)
                               /*.AddParameter("OUPath", adOuName)*/
                               .AddParameter("Credential", psCredentialObject)
                               .Invoke();
            PowerShell powershell = PowerShell.Create();
            powershell.AddCommand("Get-aduser")
                      .AddArgument(adUserId)
                      .AddParameter("Properties", "ObjectGUID");
            try
            {
                Collection<PSObject> results = powershell.Invoke();
                string guid = "";
                foreach (PSObject result in results)
                {
                    Console.WriteLine(result.ToString());
                    guid = result.ToString();
                }

                var request = new AdJoinCompleteMessage { AgentKey = _agentKey, Guid = guid, Result = "success" };
                var response = await _client.CompleteJoiningAsync(request);
                if (response != null && response.Result == "success")
                {
                    PowerShell.Create().AddCommand("Restart-Computer");
                }
            }
            catch (Exception ex) {
                Console.WriteLine("Error : " + ex.Message);
            }
        }

        private async Task AwaitPolicy()
        {
            var request = new AgentInfoMessage { VdId = _hostName };
            using (var streamingCall = _client.UpdatePolicy(request))
            {
                try
                {
                    // 스트리밍된 메시지 수신 대기
                    await foreach (var message in streamingCall.ResponseStream.ReadAllAsync())
                    {
                        Console.WriteLine($"서버로부터 받은 메시지: {message.Message}");
                        switch (message.PolicyCase)
                        {
                            case UpdatePolicyMessage.PolicyOneofCase.Clipboard:
                                Console.WriteLine($"클립보드 정책 업데이트: {message.Clipboard}");
                                break;
                            case UpdatePolicyMessage.PolicyOneofCase.UsbRedirection:
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