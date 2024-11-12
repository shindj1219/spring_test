using Grpc.Net.Client;
using GrpcClient;
using System;
using System.Threading.Tasks;
using Grpc.Core;
using System.Text.Json;
using System.Net;
using GrpcClientService;
using System.Collections.ObjectModel;
using System.Security;
using System.Management.Automation;
using System.Net.NetworkInformation;
using System.Management;
using System.Management.Automation.Runspaces;

class Program {
    static AgentService.AgentServiceClient _client;
    static string _agentKey;
    static string _hostName;
    static test_client.RdpSessionEventListener _listener;

    static async Task Main(string[] args)
    {
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
                string server_address = config.Grpc.ServerAddress;
                Console.WriteLine("Grpc server address : " + server_address);
                // gRPC 서버와의 연결 설정
                var channel = GrpcChannel.ForAddress(server_address, new GrpcChannelOptions
                {
                    HttpHandler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    }
                });

                _client = new AgentService.AgentServiceClient(channel);
            }
        }
        else
        {
            Console.WriteLine($"File load 중 오류 발생.");
        }

        // 요청 생성
        var request = new AgentInfoMessage { VdId = _hostName };
        var response = await _client.RegisterAgentAsync(request);

        if (response.AgentKey != null)
        {
            _agentKey = response.AgentKey;
        }
        else
        {
            Console.WriteLine("AgentKey is null.");
        }

        if (response.AdDomain != "" && response.AdServerIp != "" && response.AdUserId != "" && response.AdUserPassword != "" && response.AdOuName != "")
        {
            await ProcessADJoining(response.AdDomain, response.AdServerIp, response.AdUserId, response.AdUserPassword, response.AdOuName);
        }
        else
        {
            await AwaitPolicy();
        }
    }
    static async Task ProcessADJoining(string adDomain, string adServerIp, string adUserId, string adUserPassword, string adOuName)
    {
        //PowerShell.Create().AddCommand("Set-DNSClientServerAddress")
        //                   .AddParameter("InterfaceIndex", 12)
        //                   .AddParameter("ServerAddresses", adServerIp)
        //                   .Invoke();
        ChangeDNS(adServerIp);

        SecureString secureString = new SecureString();
        foreach (char c in adUserPassword)
        {
            secureString.AppendChar(c);
        }

        using (PowerShell ps = PowerShell.Create()) 
        {
            try
            {
                var psCredentialObject = new System.Management.Automation.PSCredential(adUserId, secureString);

                ps.AddCommand("Add-Computer")
                  .AddParameter("DomainName", adDomain)
                  .AddParameter("Credential", psCredentialObject);
                /*.AddParameter("OUPath", adOuName);*/
                RunPowerShellCommand(ps);

                ps.Commands.Clear();

                ps.AddCommand("Get-aduser")
                  .AddArgument(adUserId)
                  .AddParameter("Properties", "ObjectGUID");
                Collection<PSObject> results = ps.Invoke();
                string guid = "";
                if (ps.Streams.Error.Count > 0)
                {
                    Console.WriteLine("Error occurred. Error : ");
                    foreach (var error in ps.Streams.Error)
                    {
                        Console.WriteLine(error.ToString());
                    }
                }
                else
                {
                    foreach (PSObject result in results)
                    {
                        Console.WriteLine(result.ToString());
                        guid = result.ToString();
                    }
                }

                ps.Commands.Clear();

                var request = new AdJoinCompleteMessage { AgentKey = _agentKey, Guid = guid, Result = "success" };
                var response = await _client.CompleteJoiningAsync(request);
                if (response != null && response.Result == "success")
                {
                    ps.AddCommand("Restart-Computer");
                    RunPowerShellCommand(ps);
                }

            }
            catch(Exception ex)
            {
                Console.WriteLine("Error : " + ex.Message);
            }
        }
    }

    static async Task AwaitPolicy()
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

    static void ChangeDNS(string adServerIp) 
    {
        NetworkInterface? activeInterface = null;
        foreach (NetworkInterface intf in NetworkInterface.GetAllNetworkInterfaces()) {
            if ((intf.NetworkInterfaceType == NetworkInterfaceType.Ethernet || intf.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) && intf.OperationalStatus == OperationalStatus.Up) { 
                activeInterface = intf;
                break;
            }
        }

        if (activeInterface != null) {
            ManagementClass mc = new ManagementClass("Win32_NetworkAdapterConfiguration");
            ManagementObjectCollection moc = mc.GetInstances();
            foreach (ManagementObject mo in moc) {
                if ((bool)mo["IPEnabled"]) {
                    if (mo["Caption"].ToString().Contains(activeInterface.Description)) {
                        ManagementBaseObject newDNS = mo.GetMethodParameters("SetDNSServerSearchOrder");
                        string[] dnsServers = { adServerIp };
                        newDNS["DNSServerSearchOrder"] = dnsServers;
                        ManagementBaseObject result = mo.InvokeMethod("SetDNSServerSearchOrder", newDNS, null);
                        Console.WriteLine("Change DNS server successed.");
                        return;
                    }
                }
            }
            Console.WriteLine("Failed to change DNS server.");
        }
        else
        {
            Console.WriteLine("Cannot find active network interface.");
        }
    }

    static void RunPowerShellCommand(PowerShell ps)
    {
        Collection<PSObject> results = ps.Invoke();

        if (ps.Streams.Error.Count > 0)
        {
            Console.WriteLine("Error occurred. Error : ");
            foreach (var error in ps.Streams.Error)
            {
                Console.WriteLine(error.ToString());
            }
        }
        else
        {
            foreach (var result in results)
            {
                Console.WriteLine(result.ToString());
            }
        }
    }
}