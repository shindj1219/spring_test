package com.example.testserver;
import com.example.grpc.PushServiceProto;
import com.example.grpc.PushServiceGrpc;
import com.example.grpc.SubscribeRequest;
import com.example.grpc.UpdateMessage;
import io.grpc.stub.StreamObserver;
import net.devh.boot.grpc.server.service.GrpcService;

import java.time.LocalTime;

@GrpcService
public class PushServiceImpl extends PushServiceGrpc.PushServiceImplBase{
    @Override
    public void subscribeToUpdates(SubscribeRequest request,
                                   StreamObserver<UpdateMessage> responseObserver) {
        System.out.println("클라이언트 연결됨: " + request.getClientId());

        // 서버가 클라이언트에게 주기적으로 메시지를 푸시함
        for (int i = 0; i < 10; i++) {
            UpdateMessage message = UpdateMessage.newBuilder()
                    .setMessage("서버에서 보낸 메시지: " + LocalTime.now().toString())
                    .build();
            responseObserver.onNext(message);

            try {
                // 1초 대기 후 다음 메시지 푸시
                Thread.sleep(1000);
            } catch (InterruptedException e) {
                e.printStackTrace();
            }
        }

        // 스트리밍이 끝났음을 알림
        responseObserver.onCompleted();
    }
}
