using System;
using System.Threading.Tasks;
using UnityEngine;


    public class Timer
    {
        private readonly float _totalTime;
        private float _timer;
        private bool _isRunning;

        public event Action OnTimerStart;
        public event Action<float> OnTimerRunning;
        public event Action OnTimerEnd;
        public  DelayedEvent delayedEvent;
        public Timer(float totalTime)
        {
            _totalTime = totalTime;
        }

        public async void StartTimer()
        {
            if (_isRunning) return;

            _isRunning = true;
            OnTimerStart?.Invoke();

            while (_isRunning && _timer <= _totalTime)
            {
                await Task.Yield();
                UpdateTimer();
            }

            if (!_isRunning) return;

            StopTimer(true);
            OnTimerEnd?.Invoke();
        }

        public void StopTimer(bool shouldReset)
        {
            _isRunning = false;

            if (shouldReset)
            {
                _timer = 0;
            }
        }

        private void UpdateTimer()
        {
            _timer += Time.deltaTime;

            if (_timer <= _totalTime)
            {
                OnTimerRunning?.Invoke(Mathf.InverseLerp(0, _totalTime, _timer));
            }
        }
    }



