package com.example.testserver;

import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Component;

@Component
public class ScheduledTask {
    @Scheduled(fixedRate = 5000)
    public void scheduledTask() {
        System.out.println("scheduledTask");
    }
}
