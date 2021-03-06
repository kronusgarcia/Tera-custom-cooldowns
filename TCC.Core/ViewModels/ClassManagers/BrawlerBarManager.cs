﻿using TCC.Data;
using TCC.Data.Databases;

namespace TCC.ViewModels
{
    public class BrawlerBarManager : ClassManager
    {
        private static BrawlerBarManager _instance;
        private bool _isGfOn;
        private bool _counterProc;
        public static BrawlerBarManager Instance => _instance ?? (_instance = new BrawlerBarManager());
        public BrawlerBarManager() : base()
        {
            _instance = this;
            CurrentClassManager = this;
            LoadSpecialSkills();
        }

        public FixedSkillCooldown GrowingFury { get; set; }
        public FixedSkillCooldown Counter { get; set; }

        public bool IsGfOn
        {
            get => _isGfOn;
            set
            {
                if (_isGfOn == value) return;
                _isGfOn = value;
                NotifyPropertyChanged(nameof(IsGfOn));
            }
        }

        public bool CounterProc
        {
            get => _counterProc;
            set
            {
                if (_counterProc == value) return;
                _counterProc = value;
                NotifyPropertyChanged(nameof(CounterProc));
            }
        }

        protected override void LoadSpecialSkills()
        {
            
            SkillsDatabase.TryGetSkill(180100, Class.Fighter, out Skill gf);
            SkillsDatabase.TryGetSkill(21200, Class.Fighter, out Skill c);
            GrowingFury = new FixedSkillCooldown(gf, CooldownType.Skill, _dispatcher, false);
            Counter = new FixedSkillCooldown(c, CooldownType.Skill, _dispatcher, false);
        }

    }
}
