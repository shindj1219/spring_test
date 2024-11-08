package com.example.testserver;

import io.grpc.stub.StreamObserver;
import net.devh.boot.grpc.server.service.GrpcService;

import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.scheduling.TaskScheduler;

import java.util.Map;
import java.util.HashMap;
import java.util.Random;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ScheduledFuture;

@GrpcService
public class AgentServiceImpl extends AgentServiceGrpc.AgentServiceImplBase{
    @Autowired
    private TaskScheduler taskScheduler;

    // 각 클라이언트의 타이머를 관리하는 ConcurrentHashMap
    private final Map<String, ScheduledFuture<?>> clientSchedules = new ConcurrentHashMap<>();

    // vdId와 agentKey를 매핑하고 있는 map
    private final Map<String, String> agentKeyMap = new HashMap<String, String>();

    private final String adDomain = "ad.testad.com";
    private final String adServerIp = "192.168.13.98";
    private final String adUserId = "Administrator";
    private final String adUserPassword = "test123!";
    private final String adOuName = "test";

    @Override
    public void registerAgent(AgentServiceProto.AgentInfoMessage request, StreamObserver<AgentServiceProto.AgentInformationMessage> responseObserver) {
        String clientVdId = request.getVdId();
        System.out.println("클라이언트 연결됨: " + clientVdId);

        AgentServiceProto.AgentInformationMessage response;
        if(agentKeyMap.containsKey(clientVdId)) {
            response = AgentServiceProto.AgentInformationMessage.newBuilder()
                    .setAgentKey(agentKeyMap.get(clientVdId))
                    .build();
        } else {
            String newAgentKey = GenerateRandomString(10);
            agentKeyMap.put(clientVdId, newAgentKey);
            response = AgentServiceProto.AgentInformationMessage.newBuilder()
                    .setAgentKey(newAgentKey)
                    .setAdDomain(adDomain)
                    .setAdServerIp(adServerIp)
                    .setAdUserId(adUserId)
                    .setAdUserPassword(adUserPassword)
                    .setAdOuName(adOuName)
                    .build();
        }
        responseObserver.onNext(response);
        responseObserver.onCompleted();
    }

    @Override
    public void completeJoining(AgentServiceProto.AdJoinCompleteMessage request, StreamObserver<AgentServiceProto.ResultMessage> responseObserver) {
        String clientGuid = request.getGuid();
        System.out.println("클라이언트 Guid: " + clientGuid);

        AgentServiceProto.ResultMessage response = AgentServiceProto.ResultMessage.newBuilder()
                .setResult("success")
                .build();
        responseObserver.onNext(response);
        responseObserver.onCompleted();
    }

    @Override
    public void updatePolicy(AgentServiceProto.AgentInfoMessage request,
                             StreamObserver<AgentServiceProto.UpdatePolicyMessage> responseObserver) {
        String clientVdId = request.getVdId();
        System.out.println("클라이언트 연결됨: " + clientVdId);

        ScheduledFuture<?> scheduledTask = taskScheduler.scheduleAtFixedRate(() -> {
            AgentServiceProto.UpdatePolicyMessage message = AgentServiceProto.UpdatePolicyMessage.newBuilder()
                    .setMessage("서버에서 " + clientVdId + "에게 보낸 메시지: " + System.currentTimeMillis())
                    .setClipboard("on")
                    .build();
            try {
                responseObserver.onNext(message);
                System.out.println("클라이언트 " + clientVdId + "에 메시지 전송됨");
            } catch (Exception e) {
                stopSchedulerForClient(clientVdId);
            }
        }, 3000);

        clientSchedules.put(clientVdId, scheduledTask);
    }

    private String GenerateRandomString(int length) {
        int leftLimit = 97; // a
        int rightLimit = 122; // z
        Random random = new Random();
        StringBuilder buffer = new StringBuilder(length);
        for(int i = 0; i < length; ++i) {
            int randomLimitedInt = leftLimit + random.nextInt(rightLimit - leftLimit);
            buffer.append((char) randomLimitedInt);
        }
        return buffer.toString();
    }

    // 클라이언트의 스케줄 중지 메서드
    private void stopSchedulerForClient(String clientId) {
        ScheduledFuture<?> scheduledTask = clientSchedules.get(clientId);
        if (scheduledTask != null) {
            scheduledTask.cancel(true);
            clientSchedules.remove(clientId);
            System.out.println("클라이언트 " + clientId + "의 스케줄이 중지되었습니다.");
        }
    }
}
