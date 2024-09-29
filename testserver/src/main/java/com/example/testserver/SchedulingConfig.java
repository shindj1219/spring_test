package com.example.testserver;

import org.springframework.context.annotation.Configuration;
import org.springframework.scheduling.annotation.EnableScheduling;
import org.springframework.scheduling.annotation.SchedulingConfigurer;
import org.springframework.scheduling.concurrent.ThreadPoolTaskScheduler;
import org.springframework.scheduling.config.ScheduledTaskRegistrar;

@EnableScheduling
@Configuration
public class SchedulingConfig implements SchedulingConfigurer {
    // 스케쥴링 기능을 확장할 수 있는 o.s.scheduling.annotation.`SchedulingConfigurer` 인터페이스를 사용하여 재설정
    @Override
    public void configureTasks(ScheduledTaskRegistrar taskRegistrar) {
        // 2 개 이상의 태스크를 동시에 실행하려면 ThreadPoolTaskScheduler 구현 클래스 사용
        ThreadPoolTaskScheduler taskScheduler = new ThreadPoolTaskScheduler();
        taskScheduler.setPoolSize(10);  // 스레드 갯수 설정
        taskScheduler.setThreadNamePrefix("TaskScheduler-");
        taskScheduler.initialize(); // 설정을 마치면 객체 초기화 필요

        taskRegistrar.setTaskScheduler(taskScheduler);
    }
}