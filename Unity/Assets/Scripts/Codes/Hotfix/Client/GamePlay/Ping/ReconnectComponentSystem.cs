using System;

namespace ET.Client
{
    [ObjectSystem]
    public class ReconnectComponentAwakeSystem: AwakeSystem<ReconnectComponent>
    {
        protected override void Awake(ReconnectComponent self)
        {
            Log.Info($"add reconnect component");
            // await TimerComponent.Instance.WaitAsync(3000);
        }
        
    }
    
    // [ObjectSystem]
    // public class ReconnectComponentPauseSystem: PauseSystem<ReconnectComponent>
    // {
    //     protected override void Pause(ReconnectComponent self, bool isPause)
    //     {
    //         self.Pause(isPause);
    //     }
    //     
    // }
    
    [ObjectSystem]
    public class ReconnectComponentUpdateSystem: UpdateSystem<ReconnectComponent>
    {
        protected override void Update(ReconnectComponent self)
        {
            // self.Update();
        }
        
    }

    [ObjectSystem]
    public class ReconnectComponentDestroySystem: DestroySystem<ReconnectComponent>
    {
        protected override void Destroy(ReconnectComponent self)
        {
            TimerComponent.Instance.Remove(ref self.timer);
            TimerComponent.Instance.Remove(ref self.heartTimer);
        }
    }

    [Invoke(TimerInvokeType.AutoReconnect)]
    public class PlayerOfflineOutTime : ATimer<ReconnectComponent>
    {
        protected override void Run(ReconnectComponent self)
        {
            try
            {
                self.Reconnect().Coroutine();
            }
            catch (Exception e)
            {
                Log.Error($"reconnect error: {self.Id}\n{e}");
            }
        }
    }

    [Invoke(TimerInvokeType.HeartBeat)]
    public class CheckHeartBeat : ATimer<ReconnectComponent>
    {
        protected override void Run(ReconnectComponent self)
        {
            try
            {
                self.CheckHeartBeat().Coroutine();
            }
            catch (Exception e)
            {
                Log.Error($"CheckHeartBeat error: {self.Id}\n{e}");
            }
        }
    }

    [FriendOf(typeof(ReconnectComponent))]
    public static class ReconnectComponentSystem
    {
        public static void Pause(this ReconnectComponent self, bool isPause)
        {
            Log.Info($"reconnect pause:{isPause}");
            //���ⲿ�л�����ʱ���ж���ʱ���Ƿ�������
        }
        
        public static void StartHeartBeat(this ReconnectComponent self)
        {
            self.heartTimer = TimerComponent.Instance.NewRepeatedTimer(2000, TimerInvokeType.HeartBeat, self);
        }
        
        public static async ETTask CheckHeartBeat(this ReconnectComponent self)
        {
            // Log.Info($"heart beat, heart off count:{self?.heartOffCount}");
            // if (Game.IsOffLine)
            //     return;
            if (!Game.IsOffLine && self.heartOffCount > 4)
            {
                self.StartReconnect();
            }
            Session session = self.DomainScene().GetComponent<SessionComponent>().Session;
            if (session != null)
            {
                long time1 = TimeHelper.ClientNow();
                try
                {
                    G2C_Ping response = await session.Call(new C2G_Ping()) as G2C_Ping;
                    self.heartOffCount = 0;
                    Log.Info($"heart beat, {TimeInfo.Instance.ClientNow()} ");
                    // if (self.InstanceId != instanceId)
                    // {
                    //     return;
                    // }

                    // long time2 = TimeHelper.ClientNow();
                    //
                    // TimeInfo.Instance.ServerMinusClientTime = response.Time + (time2 - time1) / 2 - time2;

                }
                catch (RpcException e)
                {
                    // session�Ͽ�����ping rpc������¼һ�¼��ɣ�����Ҫ���error
                    Log.Info($"reconnect ping error: {self.Id} {e.Error}");
                    self.heartOffCount++;
                }
                catch (Exception e)
                {
                    Log.Error($"ping error: \n{e}");
                    self.heartOffCount++;
                }
            }
            else
            {
                self.heartOffCount++;
            }
        }
        
        public static void StartReconnect(this ReconnectComponent self)
        {
            if (self.timer != 0)
            {
                return;
            }

            Game.IsOffLine = true;
            Log.Info($"start reconnect");
            //��������Զ�����
            self.timer = TimerComponent.Instance.NewRepeatedTimer(self.reconnectInterval, TimerInvokeType.AutoReconnect, self);
            self.Reconnect().Coroutine();
        }
        public static async ETTask Reconnect(this ReconnectComponent self)
        {
            //��������Զ����Ӵ������򵯴�����ʧ�ܡ�
            if (self.reconnectTimes >= self.maxAutoReconnectTimes)
            {
                if (!self.showMsgBox)
                {
                    self.showMsgBox = true;
                    // await EventSystem.Instance.PublishAsync(self.DomainScene(), new EventType.PlayerOffline() { Message = "��������ʧ�ܣ��������������ٽ�����Ϸ" });
                }
                return;
            }
            Log.Info($"auto reconnect:��{self.reconnectTimes}��, ���{self.reconnectInterval}ms");
            PlayerComponent player = self.ClientScene().GetComponent<PlayerComponent>();
            string account = player.Account;
            string password = player.Password;
            int intRes = await LoginHelper.Login(self.DomainScene(),
                account,
                password, true);
            //���ӳɹ���ȡ���Զ�������ʱ����������������
            if (intRes == ErrorCode.ERR_Success)
            {
                Log.Error($"�����ɹ�");
                TimerComponent.Instance.Remove(ref self.timer);
                Game.IsOffLine = false;
                self.timer = 0;
                self.showMsgBox = false;
                self.heartOffCount = 0;
                self.reconnectTimes = 0;
                return;
            }

            self.reconnectTimes++;
        }
    }
}