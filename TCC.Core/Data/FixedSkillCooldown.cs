﻿using System;
using System.Windows.Threading;
namespace TCC.Data
{
    public class FixedSkillCooldown : TSPropertyChanged
    {
        CooldownType _type;
        DispatcherTimer _secondsTimer;
        DispatcherTimer _offsetTimer;
        DispatcherTimer _shortTimer;
        public Skill Skill { get; set; }
        ulong cooldown;
        public ulong Cooldown
        {
            get => cooldown; set
            {
                if (cooldown == value) return;
                cooldown = value;
            }
        }
        public ulong OriginalCooldown { get; set; }
        bool isAvailable = true;
        public bool IsAvailable
        {
            get => isAvailable;
            private set
            {
                if (isAvailable == value) return;
                isAvailable = value;
                NotifyPropertyChanged("IsAvailable");
            }
        }

        ulong seconds = 0;
        private bool _flashOnAvailable;

        public ulong Seconds
        {
            get
            {
                return seconds;
            }
            set
            {
                if (seconds == value) return;
                seconds = value;
                NotifyPropertyChanged("Seconds");
            }
        }

        public bool FlashOnAvailable
        {
            get { return _flashOnAvailable; }
            set
            {
                if (_flashOnAvailable == value) return;
                _flashOnAvailable = value;
                NotifyPropertyChanged(nameof(FlashOnAvailable));
                NotifyPropertyChanged(nameof(IsAvailable));
            }
        }

        public FixedSkillCooldown(Skill sk, CooldownType t, Dispatcher d, bool flashOnAvailable)
        {
            _dispatcher = d;

            _shortTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher);
            _shortTimer.Tick += _shortTimer_Tick;

            Seconds = 0;

            _secondsTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher);
            _secondsTimer.Interval = TimeSpan.FromSeconds(1);
            _secondsTimer.Tick += _secondsTimer_Tick;

            _offsetTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher);
            _offsetTimer.Tick += _offsetTimer_Tick;

            _type = t;
            Skill = sk;

            FlashOnAvailable = flashOnAvailable;
        }

        private void _shortTimer_Tick(object sender, EventArgs e)
        {
            _shortTimer.Stop();
            IsAvailable = true;
        }

        private void _offsetTimer_Tick(object sender, EventArgs e)
        {
            _offsetTimer.Stop();
            Seconds--;
            _secondsTimer.Start();

        }

        public FixedSkillCooldown(Dispatcher d)
        {
            _dispatcher = d;
        }
        private void _secondsTimer_Tick(object sender, EventArgs e)
        {
            var s = Seconds;
            if(Seconds == 1)
            {
                _secondsTimer.Stop();
                IsAvailable = true;
            }
            Seconds--;
            if (Seconds > s)
            {
                _secondsTimer.Stop();
                IsAvailable = true;
            }
        }

        public void Start(ulong cd)
        {
            if (cd > 1000)
            {
                Cooldown = cd;//_type == CooldownType.Skill ? cd : cd * 1000;
                OriginalCooldown = Cooldown;
                Seconds = 1 + (Cooldown / 1000);
                var offset = Cooldown % 1000;
                _offsetTimer.Interval = TimeSpan.FromMilliseconds(offset);
                _offsetTimer.Start();
                IsAvailable = false;
            }
            else
            {
                Cooldown = cd;
                _shortTimer.Interval = TimeSpan.FromMilliseconds(cd);
                _shortTimer.Start();
                Seconds = 0;
                IsAvailable = false;
            }
            NotifyPropertyChanged("Start");

        }
        public void Refresh(uint cd)
        {
            _secondsTimer.Stop();
            if (cd == 0)
            {
                IsAvailable = true;
                Cooldown = 0;
                Seconds = 0;
                NotifyPropertyChanged("Refresh");
                return;
            }
            Cooldown = cd;
            Seconds = Cooldown / 1000;
            if(Seconds == 0)
            {
                _secondsTimer.Stop();
                IsAvailable = true;
                return;
            }
            _offsetTimer.Interval = TimeSpan.FromMilliseconds(Cooldown % 1000);
            _offsetTimer.Start();
            NotifyPropertyChanged("Refresh");
        }
        public void ForceAvailable(bool available)
        {
            IsAvailable = available;
        }
    }
}
