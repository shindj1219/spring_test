package com.example.testserver;

import com.example.grpc.PushServiceProto;
import com.example.grpc.PushServiceGrpc;
import com.example.grpc.SubscribeRequest;
import com.example.grpc.UpdateMessage;
import io.grpc.stub.StreamObserver;
import net.devh.boot.grpc.server.service.GrpcService;

import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.scheduling.TaskScheduler;

import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ScheduledFuture;

@GrpcService
public class PushServiceImpl extends PushServiceGrpc.PushServiceImplBase{
    @Autowired
    private TaskScheduler taskScheduler;

    // 각 클라이언트의 타이머를 관리하는 ConcurrentHashMap
    private final Map<String, ScheduledFuture<?>> clientSchedules = new ConcurrentHashMap<>();

    @Override
    public void subscribeToUpdates(SubscribeRequest request,
                                   StreamObserver<UpdateMessage> responseObserver) {
        String clientId = request.getClientId();
        System.out.println("클라이언트 연결됨: " + clientId);

        ScheduledFuture<?> scheduledTask = taskScheduler.scheduleAtFixedRate(() -> {
            UpdateMessage message = UpdateMessage.newBuilder()
                    .setMessage("서버에서 " + clientId + "에게 보낸 메시지: " + System.currentTimeMillis())
                    .build();
            try {
                responseObserver.onNext(message);
                System.out.println("클라이언트 " + clientId + "에 메시지 전송됨");
            } catch (Exception e) {
                stopSchedulerForClient(clientId);
            }
        }, 5000);

        clientSchedules.put(clientId, scheduledTask);
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
