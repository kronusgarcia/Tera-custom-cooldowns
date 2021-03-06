﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using TCC.Data;
using TCC.Parsing;

namespace TCC.ViewModels
{
    public class BossGageWindowViewModel : TccWindowViewModel
    {
        public const int Ph1ShieldDuration = 16;
        private static BossGageWindowViewModel _instance;
        private HarrowholdPhase _currentHHphase;
        private ICollectionView _bams;
        private ICollectionView _mobs;
        private ICollectionView _dragons;
        private ICollectionView _guildTowers;

        private Npc _selectedDragon;
        private Npc _vergos;
        private SynchronizedObservableCollection<Npc> _npcList;

        private readonly List<Npc> _holdedDragons = new List<Npc>();
        private readonly Dictionary<ulong, string> _towerNames = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, float> _savedHp = new Dictionary<ulong, float>();

        private void AddSortedDragons()
        {
            _npcList.Add(_holdedDragons.First(x => x.TemplateId == 1102));
            _npcList.Add(_holdedDragons.First(x => x.TemplateId == 1100));
            _npcList.Add(_holdedDragons.First(x => x.TemplateId == 1101));
            _npcList.Add(_holdedDragons.First(x => x.TemplateId == 1103));
            _holdedDragons.Clear();
        }

        public bool IsTeraOnTop => WindowManager.IsTccVisible;

        public int VisibleBossesCount => NpcList.Count(x => x.Visible == Visibility.Visible);

        public HarrowholdPhase CurrentHHphase
        {
            get => _currentHHphase;
            set
            {
                if (_currentHHphase == value) return;
                _currentHHphase = value;
                MessageFactory.Update();
                NotifyPropertyChanged(nameof(CurrentHHphase));
            }
        }
        public ICollectionView Bams
        {
            get
            {
                _bams = new CollectionViewSource { Source = _npcList }.View;
                _bams.Filter = p => ((Npc)p).IsBoss && !((Npc)p).IsTower;
                return _bams;
            }
        }
        public ICollectionView Mobs
        {
            get
            {
                _mobs = new CollectionViewSource { Source = _npcList }.View;
                _mobs.Filter = p => !((Npc)p).IsBoss && !((Npc)p).IsTower;
                return _mobs;
            }
        }
        public ICollectionView GuildTowers
        {
            get
            {
                _guildTowers = new CollectionViewSource { Source = _npcList }.View;
                _guildTowers.Filter = p => ((Npc)p).IsTower;
                return _guildTowers;
            }
        }
        public ICollectionView Dragons
        {
            get
            {
                _dragons = new CollectionViewSource { Source = _npcList }.View;
                _dragons.Filter = p => ((Npc)p).TemplateId > 1099 && ((Npc)p).TemplateId < 1104;
                return _dragons;
            }
        }
        public Npc SelectedDragon
        {
            get => _selectedDragon;
            set
            {
                if (_selectedDragon == value) return;
                _selectedDragon = value;
                NotifyPropertyChanged(nameof(SelectedDragon));
            }
        }
        public Npc Vergos
        {
            get => _vergos;
            set
            {
                if (_vergos == value) return;
                _vergos = value;
                NotifyPropertyChanged(nameof(Vergos));
            }
        }
        public SynchronizedObservableCollection<Npc> NpcList
        {
            get
            {
                return _npcList;
            }
            set
            {
                if (_npcList == value) return;
                _npcList = value;
            }
        }
        public static BossGageWindowViewModel Instance => _instance ?? (_instance = new BossGageWindowViewModel());
        public Dictionary<ulong, uint> GuildIds { get; private set; }

        public BossGageWindowViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _scale = SettingsManager.BossWindowSettings.Scale;
            _npcList = new SynchronizedObservableCollection<Npc>(_dispatcher);

            GuildIds = new Dictionary<ulong, uint>();
            WindowManager.TccVisibilityChanged += (s, ev) =>
            {
                NotifyPropertyChanged(nameof(IsTeraOnTop));
                if (IsTeraOnTop)
                {
                    WindowManager.BossWindow.RefreshTopmost();
                }
            };

        }


        public void AddOrUpdateBoss(ulong entityId, float maxHp, float curHp, bool isBoss, uint templateId = 0, uint zoneId = 0, Visibility v = Visibility.Visible)
        {
            var boss = _npcList.FirstOrDefault(x => x.EntityId == entityId);
            if (boss == null)
            {
                if (SettingsManager.ShowOnlyBosses && !isBoss) return;

                if (templateId == 0 || zoneId == 0) return;

                v = Utils.IsGuildTower(zoneId, templateId) ? Visibility.Visible : v;
                boss = new Npc(entityId, zoneId, templateId, isBoss, v);
                if (boss.IsTower)
                {
                    if (_towerNames.TryGetValue(entityId, out var towerName))
                    {
                        boss.Name = towerName;
                    }
                    boss.IsBoss = true;
                }
                if (boss.IsPhase1Dragon)
                {
                    var d = _holdedDragons.FirstOrDefault(x => x.EntityId == entityId);
                    if (d == null)
                    {
                        _holdedDragons.Add(boss);
                        if (_holdedDragons.Count == 4)
                        {
                            AddSortedDragons();
                        }
                    }
                }
                else
                {
                    if (boss.ZoneId == 950 && (boss.TemplateId == 1000 || boss.TemplateId == 2000 || boss.TemplateId == 3000 || boss.TemplateId == 4000)) Vergos = boss;
                    if (_savedHp.ContainsKey(boss.EntityId)) boss.CurrentHP = _savedHp[boss.EntityId];
                    _npcList.Add(boss);
                }

            }
            boss.MaxHP = maxHp;
            boss.CurrentHP = curHp;
            boss.Visible = v;
        }
        public void RemoveBoss(ulong id, DespawnType type)
        {
            var boss = _npcList.FirstOrDefault(x => x.EntityId == id);
            if (boss == null) return;
            if (type == DespawnType.OutOfView)
            {
                if (!_savedHp.ContainsKey(id)) _savedHp.Add(id, boss.CurrentHP);
            }
            else
            {
                if (_savedHp.ContainsKey(id)) _savedHp.Remove(id);
            }
            if (boss.Visible != Visibility.Visible || boss.IsTower)
            {
                NpcList.Remove(boss);
                boss.Dispose();
            }
            else
            {
                boss.Delete();
            }
            //_currentNPCs.Remove(boss);
            //boss.Dispose();
            if (SelectedDragon != null && SelectedDragon.EntityId == id) SelectedDragon = null;
        }
        public void CopyToClipboard()
        {
            var sb = new StringBuilder();
            foreach (var boss in _npcList)
            {
                if (boss.Visible == Visibility.Visible)
                {
                    sb.Append(boss.Name);
                    sb.Append(": ");
                    sb.Append(String.Format("{0:##0%}", boss.CurrentFactor));
                    sb.Append("\\");
                }
            }
            Clipboard.SetText(sb.ToString());
        }
        public void ClearBosses()
        {
            _npcList.Clear();
        }
        public void EndNpcAbnormality(ulong target, Abnormality ab)
        {
            var boss = _npcList.FirstOrDefault(x => x.EntityId == target);
            if (boss != null)
            {
                boss.EndBuff(ab);
            }
        }
        public void AddOrRefreshNpcAbnormality(Abnormality ab, int stacks, uint duration, ulong target)
        {
            var boss = _npcList.FirstOrDefault(x => x.EntityId == target);
            boss?.AddorRefresh(ab, duration, stacks);
        }
        public void SetBossEnrage(ulong entityId, bool enraged)
        {
            var boss = _npcList.FirstOrDefault(x => x.EntityId == entityId);
            if (boss == null) return;
            boss.Enraged = enraged;
        }
        public void UnsetBossTarget(ulong entityId)
        {
            var boss = _npcList.FirstOrDefault(x => x.EntityId == entityId);
            if (boss == null)
            {
                return;
            }
            boss.Target = 0;
        }
        public void SetBossAggro(ulong entityId, AggroCircle circle, ulong user)
        {
            var boss = _npcList.FirstOrDefault(x => x.EntityId == entityId);
            if (boss == null)
            {
                return;
            }
            boss.Target = user;
            boss.CurrentAggroType = AggroCircle.Main;
        }
        public void SelectDragon(Dragon dragon)
        {
            foreach (var item in _npcList.Where(x => x.TemplateId > 1099 && x.TemplateId < 1104))
            {
                if (item.TemplateId == (uint)dragon) { item.IsSelected = true; SelectedDragon = item; }
                else item.IsSelected = false;
            }
        }
        public void ClearGuildTowers()
        {
            _towerNames.Clear();
        }
        public void AddGuildTower(ulong towerId, string guildName, uint guildId)
        {
            if (!GuildIds.ContainsKey(towerId)) GuildIds.Add(towerId, guildId);
            var t = NpcList.FirstOrDefault(x => x.EntityId == towerId);
            if (t != null) t.Name = guildName;
            if (_towerNames.ContainsKey(towerId)) return;
            _towerNames.Add(towerId, guildName);
        }

        public void UpdateShield(ulong target, uint damage)
        {
            var boss = _npcList.FirstOrDefault(x => x.EntityId == target);
            if (boss != null)
            {
                boss.CurrentShield -= damage;
            }
        }

        public void RemoveMe(Npc npc)
        {
            _dispatcher.Invoke(() =>
            {
                var b = NpcList.FirstOrDefault(x => x == npc);
                if (b == null) return;
                b.Buffs.Clear();
                var dt = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(5500)
                };
                dt.Tick += (s, ev) =>
                {
                    dt.Stop();
                    NpcList.Remove(b);
                    b.Dispose();
                };
                dt.Start();
            });
        }
    }
}
