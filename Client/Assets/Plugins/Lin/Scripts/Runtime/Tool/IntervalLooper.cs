/*
┌────────────────────────────┐
│　Description: 间隔执行器
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: IntervalLooper
└──────────────┘
*/

using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace Lin.Runtime.Tool
{
    public class IntervalLooper : IDisposable
    {
        public int executedTimes = 0;
        private int loopTimes = -1;
        private double accumulatedTime;
        private readonly float interval;
        private readonly Action action;
        private bool ignoreTimeScale;

        private const int INFINITE = -1;

        private CancellationTokenSource byPause, byStop;

        public ELoopState loopState { get; private set; }

        public IntervalLooper(float interval, Action action)
        {
            if (interval <= 1 / 500f)
                throw new Exception($"{nameof(interval)} is too small.");

            if (action is null)
                throw new NullReferenceException($"{nameof(action)} is null.");

            this.interval = interval;
            this.action = action;
            loopState = ELoopState.UNSTART;
        }


        public void Start(bool ignoreTimeScale = true) => Start(INFINITE, ignoreTimeScale);

        public void Start(int loopTimes, bool ignoreTimeScale = true)
        {
            byPause?.Dispose();
            byStop?.Dispose();

            this.loopTimes = loopTimes;
            this.ignoreTimeScale = ignoreTimeScale;
            byPause = new CancellationTokenSource();
            byStop = new CancellationTokenSource();
            executedTimes = 0;
            accumulatedTime = 0;

            RunAsync();
        }

        private async void RunAsync()
        {
            while (byStop is not null && !byStop.IsCancellationRequested)
            {
                while (loopState == ELoopState.PAUSED)
                {
                    try
                    {
                        await UniTask.Delay(10, ignoreTimeScale, cancellationToken: byStop.Token);
                    }
                    catch (Exception)
                    {
                        break;
                    }
                }

                try
                {
                    DateTime start = DateTime.Now;
                    await UniTask.Delay(10, ignoreTimeScale, cancellationToken: byPause.Token, cancelImmediately: true).AttachExternalCancellation(byStop.Token);
                    accumulatedTime += (DateTime.Now - start).TotalSeconds;
                    if (accumulatedTime >= interval)
                    {
                        accumulatedTime -= interval;
                        action();

                        executedTimes++;
                        if (executedTimes >= loopTimes)
                        {
                            Stop();
                        }
                    }
                }
                catch (Exception ex) { UnityEngine.Debug.LogWarning($"[IntervalLooper] {ex.Message}"); }
            }
        }

        public void Resume()
        {
            if (loopState != ELoopState.PAUSED)
                return;

            loopState = ELoopState.RUNNING;
        }

        public void Pause()
        {
            if (loopState != ELoopState.RUNNING)
                return;

            byPause.Cancel();
            byPause.Dispose();
            byPause = new CancellationTokenSource();
            loopState = ELoopState.PAUSED;
        }

        public void Stop()
        {
            if (loopState == ELoopState.UNSTART)
                return;

            byPause.Dispose();
            byPause = null;
            byStop.Cancel();
            byStop.Dispose();
            byStop = null;
            loopState = ELoopState.UNSTART;
        }

        public void Dispose()
        {
            byPause?.Dispose();
            byPause = null;
            byStop?.Dispose();
            byStop = null;
        }

        public enum ELoopState
        {
            UNSTART,
            RUNNING,
            PAUSED,
        }
    }
}