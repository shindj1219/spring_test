using System;
using System.Management;
using System.Threading;
using System.Threading.Tasks;

namespace test_client
{
    public class RdpSessionEventListener
    {
        private ManagementEventWatcher logonWatcher;
        private ManagementEventWatcher logoffWatcher;
        private ManagementEventWatcher lockWatcher;
        private ManagementEventWatcher unlockWatcher;

        public void Start()
        {
            // RDP 세션의 로그온 이벤트 감지
            string logonQuery = "SELECT * FROM __InstanceCreationEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_LogonSession'";
            logonWatcher = new ManagementEventWatcher(logonQuery);
            logonWatcher.EventArrived += OnLogonEvent;
            logonWatcher.Start();

            // RDP 세션의 로그오프 이벤트 감지
            string logoffQuery = "SELECT * FROM __InstanceDeletionEvent WITHIN 1 WHERE TargetInstance ISA 'Win32_LogonSession'";
            logoffWatcher = new ManagementEventWatcher(logoffQuery);
            logoffWatcher.EventArrived += OnLogoffEvent;
            logoffWatcher.Start();

            // 세션 잠금 이벤트 감지
            string lockQuery = "SELECT * FROM Win32_Session WHERE SessionState = 'Locked'";
            lockWatcher = new ManagementEventWatcher(lockQuery);
            lockWatcher.EventArrived += OnSessionLock;
            lockWatcher.Start();

            // 세션 잠금 해제 이벤트 감지
            string unlockQuery = "SELECT * FROM Win32_Session WHERE SessionState = 'Unlocked'";
            unlockWatcher = new ManagementEventWatcher(unlockQuery);
            unlockWatcher.EventArrived += OnSessionUnlock;
            unlockWatcher.Start();

            Console.WriteLine("RDP 세션 이벤트 감지를 시작합니다.");
        }

        public void Stop()
        {
            logonWatcher.Stop();
            logoffWatcher.Stop();
            lockWatcher.Stop();
            unlockWatcher.Stop();

            Console.WriteLine("RDP 세션 이벤트 감지를 중지합니다.");
        }

        private void OnLogonEvent(object sender, EventArrivedEventArgs e)
        {
            Console.WriteLine("사용자가 로그온했습니다.");
        }

        private void OnLogoffEvent(object sender, EventArrivedEventArgs e)
        {
            Console.WriteLine("사용자가 로그오프했습니다.");
        }

        private void OnSessionLock(object sender, EventArrivedEventArgs e)
        {
            Console.WriteLine("세션이 잠겼습니다.");
        }

        private void OnSessionUnlock(object sender, EventArrivedEventArgs e)
        {
            Console.WriteLine("세션이 해제되었습니다.");
        }
    }
}
