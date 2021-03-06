﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Threading;
using TCC.Data;
using TCC.Data.Databases;
using TCC.Parsing;
using TCC.Parsing.Messages;

namespace TCC.ViewModels
{
    public class GroupWindowViewModel : TccWindowViewModel
    {
        private static GroupWindowViewModel _instance;
        private bool _raid;
        private ulong _aggroHolder;
        private bool _firstCheck = true;
        private readonly object _lock = new object();
        public event Action SettingsUpdated;


        public static GroupWindowViewModel Instance => _instance ?? (_instance = new GroupWindowViewModel());
        public bool IsTeraOnTop => WindowManager.IsTccVisible; //TODO: is this needed? need to check for all VM
        public SynchronizedObservableCollection<User> Members { get; }
        public ICollectionViewLiveShaping Dps { get; }
        public ICollectionViewLiveShaping Tanks { get; }
        public ICollectionViewLiveShaping Healers { get; }
        public bool Raid
        {
            get => _raid;
            set
            {
                if (_raid == value) return;
                _raid = value;
                NotifyPropertyChanged(nameof(Raid));
            }
        }
        public int Size => Members.Count;
        public int ReadyCount => Members.Count(x => x.Ready == ReadyStatus.Ready);
        public int AliveCount => Members.Count(x => x.Alive);
        public bool Formed => Size > 0;
        public bool Rolling { get; set; }

        public GroupWindowViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _scale = SettingsManager.GroupWindowSettings.Scale;

            WindowManager.TccVisibilityChanged += (s, ev) =>
            {
                NotifyPropertyChanged("IsTeraOnTop");
                if (IsTeraOnTop)
                {
                    WindowManager.GroupWindow.RefreshTopmost();
                }
            };

            Members = new SynchronizedObservableCollection<User>(_dispatcher);
            Members.CollectionChanged += Members_CollectionChanged;

            Dps = InitLiveView(o => ((User)o).Role == Role.Dps);
            Tanks = InitLiveView(o => ((User)o).Role == Role.Tank);
            Healers = InitLiveView(o => ((User)o).Role == Role.Healer);

            ICollectionViewLiveShaping InitLiveView(Predicate<object> predicate)
            {
                var cv = new CollectionViewSource { Source = Members }.View;
                cv.Filter = predicate;
                var liveView = cv as ICollectionViewLiveShaping;
                if (!liveView.CanChangeLiveFiltering) return null;
                liveView.LiveFilteringProperties.Add(nameof(User.Role));
                liveView.IsLiveFiltering = true;
                return liveView;
            }
        }

        private void Members_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Task.Delay(100).ContinueWith(t => NotifyPropertyChanged(nameof(Size)));
            NotifyPropertyChanged(nameof(Formed));
            NotifyPropertyChanged(nameof(AliveCount));
            NotifyPropertyChanged(nameof(ReadyCount));

        }
        public void NotifySettingUpdated()
        {
            SettingsUpdated?.Invoke();
        }
        public bool Exists(ulong id)
        {
            return Members.Any(x => x.EntityId == id);
        }
        public bool Exists(string name)
        {
            return Members.Any(x => x.Name == name);
        }
        public bool Exists(uint pId, uint sId)
        {
            return Members.Any(x => x.PlayerId == pId && x.ServerId == sId);
        }

        public bool TryGetUser(string name, out User u)
        {
            u = Exists(name) ? Members.FirstOrDefault(x => x.Name == name) : null;
            return Exists(name);
        }
        public bool TryGetUser(ulong id, out User u)
        {
            u = Exists(id) ? Members.FirstOrDefault(x => x.EntityId == id) : null;
            return Exists(id);
        }
        public bool TryGetUser(uint pId,uint sId, out User u)
        {
            u = Exists(pId, sId) ? Members.FirstOrDefault(x => x.PlayerId == pId && x.ServerId == sId) : null;
            return Exists(pId, sId);
        }

        public bool IsLeader(string name)
        {
            return Members.FirstOrDefault(x => x.Name == name)?.IsLeader ?? false;
        }
        public bool HasPowers(string name)
        {
            return Members.FirstOrDefault(x => x.Name == name)?.CanInvite ?? false;
        }
        public bool AmILeader()
        {
            return IsLeader(SessionManager.CurrentPlayer.Name);
        }
        public void SetAggro(ulong target)
        {
            if (target == 0)
            {
                Members.ToList().ForEach(user => user.HasAggro = false);
                return;
            }
            if (_aggroHolder == target) return;
            Members.ToList().ForEach(user => user.HasAggro = user.EntityId == target);
        }
        public void SetAggroCircle(S_USER_EFFECT p)
        {
            if (BossGageWindowViewModel.Instance.CurrentHHphase != HarrowholdPhase.None) return;

            if (p.Circle != AggroCircle.Main) return;
            if (p.Action == AggroAction.Add)
            {
                SetAggro(p.User);
            }
        }
        public void BeginOrRefreshAbnormality(Abnormality ab, int stacks, uint duration, uint playerId, uint serverId)
        {
            if (ab.Infinity) duration = uint.MaxValue;
            var u = Members.ToArray().FirstOrDefault(x => x.ServerId == serverId && x.PlayerId == playerId);
            if (u == null) return;

            if (ab.Type == AbnormalityType.Buff)
            {
                u.AddOrRefreshBuff(ab, duration, stacks);
                if (u.UserClass == Class.Warrior && ab.Id >= 100200 && ab.Id <= 100203)
                {
                    u.Role = Role.Tank; //def stance turned on: switch warrior to tank 
                }
            }
            else
            {
                // -- show only aggro stacks if we are in HH -- //
                if (BossGageWindowViewModel.Instance.CurrentHHphase >= HarrowholdPhase.Phase2)
                {
                    if (ab.Id != 950023 && SettingsManager.ShowOnlyAggroStacks) return;
                }
                // -------------------------------------------- //
                u.AddOrRefreshDebuff(ab, duration, stacks);
            }
        }
        public void EndAbnormality(Abnormality ab, uint playerId, uint serverId)
        {
            var u = Members.FirstOrDefault(x => x.PlayerId == playerId && x.ServerId == serverId);
            if (u == null) return;

            if (ab.Type == AbnormalityType.Buff)
            {
                u.RemoveBuff(ab);
                if (u.UserClass == Class.Warrior && ab.Id >= 100200 && ab.Id <= 100203)
                {
                    u.Role = Role.Dps; //def stance ended: make warrior dps again
                }
            }
            else
            {
                u.RemoveDebuff(ab);
            }



        }
        public void ClearAbnormality(uint playerId, uint serverId)
        {
            Members.FirstOrDefault(x => x.PlayerId == playerId && x.ServerId == serverId)?.ClearAbnormalities();
        }

        public void AddOrUpdateMember(User p)
        {
            if (SettingsManager.IgnoreMeInGroupWindow && p.IsPlayer) return;
            lock (_lock)
            {
                var user = Members.FirstOrDefault(x => x.PlayerId == p.PlayerId && x.ServerId == p.ServerId);
                if (user == null)
                {
                    Members.Add(p);
                    SendAddMessage(p.Name);
                    return;
                }
                user.Online = p.Online;
                user.EntityId = p.EntityId;
                user.IsLeader = p.IsLeader;
            }
        }
        private void SendAddMessage(string name)
        {
            if(App.Debug) return;
            string msg;
            string opcode;
            if (Raid)
            {
                msg = "@0\vPartyPlayerName\v" + name;
                opcode = "SMT_JOIN_UNIONPARTY_PARTYPLAYER";
            }
            else
            {
                opcode = "SMT_JOIN_PARTY_PARTYPLAYER";
                msg = "@0\vPartyPlayerName\v" + name + "\vparty\vparty";
            }
            SystemMessages.Messages.TryGetValue(opcode, out var m);
            SystemMessagesProcessor.AnalyzeMessage(msg, m, opcode);
        }
        private void SendLeaveMessage(string name)
        {
            string msg;
            string opcode;
            if (Raid)
            {
                msg = "@0\vPartyPlayerName\v" + name;
                opcode = "SMT_LEAVE_UNIONPARTY_PARTYPLAYER";
            }
            else
            {
                opcode = "SMT_LEAVE_PARTY_PARTYPLAYER";
                msg = "@0\vPartyPlayerName\v" + name + "\vparty\vparty";
            }
            SystemMessages.Messages.TryGetValue(opcode, out SystemMessage m);
            SystemMessagesProcessor.AnalyzeMessage(msg, m, opcode);

        }
        public void RemoveMember(uint playerId, uint serverId, bool kick = false)
        {
            var u = Members.FirstOrDefault(x => x.PlayerId == playerId && x.ServerId == serverId);
            if(u == null) return;
            Members.Remove(u);
            if (!kick) SendLeaveMessage(u.Name);
        }
        public void ClearAll()
        {
            if (!SettingsManager.GroupWindowSettings.Enabled || !_dispatcher.Thread.IsAlive) return;
            Members.Clear();
            Raid = false;
        }
        public void LogoutMember(uint playerId, uint serverId)
        {
            var u = Members.FirstOrDefault(x => x.PlayerId == playerId && x.ServerId == serverId);
            if(u == null) return;
            u.Online = false;
        }
        public void RemoveMe()
        {
            var me = Members.FirstOrDefault(x => x.IsPlayer);
            if (me != null) Members.Remove(me);
        }
        public void ClearAllBuffs()
        {
            Members.ToList().ForEach(x =>
            {
                x.Buffs.ToList().ForEach(b => b.Dispose());
                x.Buffs.Clear();
            });
        }
        internal void ClearAllAbnormalities()
        {
            Members.ToList().ForEach(x =>
            {
                x.Buffs.ToList().ForEach(b => b.Dispose());
                x.Buffs.Clear();
                x.Debuffs.ToList().ForEach(b => b.Dispose());
                x.Debuffs.Clear();
            });
        }
        public void SetNewLeader(ulong entityId, string name)
        {
            Members.ToList().ForEach(x => x.IsLeader = x.Name == name);
        }
        public void StartRoll()
        {
            Rolling = true;
            Members.ToList().ForEach(u => u.IsRolling = true);
        }
        public void SetRoll(ulong entityId, int rollResult)
        {
            if (rollResult == int.MaxValue) rollResult = -1;
            var u = Members.FirstOrDefault(x => x.EntityId == entityId);
            if (u != null) u.RollResult = rollResult;
        }
        public void EndRoll()
        {
            Rolling = false;
            Members.ToList().ForEach(u =>
            {
                u.IsRolling = false;
                u.IsWinning = false;
                u.RollResult = 0;
            });
        }
        private void FindHighestRoll()
        {
            Members.ToList().ForEach(user => user.IsWinning = user.EntityId == Members.OrderByDescending(u => u.RollResult).First().EntityId);
        }
        public void SetReadyStatus(ReadyPartyMember p)
        {
            if (_firstCheck)
            {
                Members.ToList().ForEach(u => u.Ready = ReadyStatus.Undefined);
            }
            var user = Members.FirstOrDefault(u => u.PlayerId == p.PlayerId && u.ServerId == p.ServerId);
            if (user != null) user.Ready = p.Status;
            _firstCheck = false;
            NotifyPropertyChanged(nameof(ReadyCount));
        }
        public void EndReadyCheck()
        {
            Task.Delay(4000).ContinueWith(t =>
            {
                Members.ToList().ForEach(x => x.Ready = ReadyStatus.None);
            });
            _firstCheck = true;
        }
        public void UpdateMemberHp(uint playerId, uint serverId, int curHp, int maxHp)
        {
            var u = Members.FirstOrDefault(x => x.PlayerId == playerId && x.ServerId == serverId);
            if (u == null) return;
            u.CurrentHp = curHp;
            u.MaxHp = maxHp;
        }
        public void UpdateMemberMp(uint playerId, uint serverId, int curMp, int maxMp)
        {
            var u = Members.FirstOrDefault(x => x.PlayerId == playerId && x.ServerId == serverId);
            if (u == null) return;
            u.CurrentMp = curMp;
            u.MaxMp = maxMp;
        }
        public void SetRaid(bool raid)
        {
            _dispatcher.Invoke(() => Raid = raid);
        }
        public void UpdateMember(S_PARTY_MEMBER_STAT_UPDATE p)
        {
            var u = Members.FirstOrDefault(x => x.PlayerId == p.PlayerId && x.ServerId == p.ServerId);
            if (u != null)
            {
                u.CurrentHp = p.CurrentHP;
                u.CurrentMp = p.CurrentMP;
                u.MaxHp = p.MaxHP;
                u.MaxMp = p.MaxMP;
                u.Level = (uint)p.Level;
                u.Alive = p.Alive;
                NotifyPropertyChanged(nameof(AliveCount));
                if (!p.Alive) u.HasAggro = false;
            }
        }
        public void NotifyThresholdChanged()
        {
            NotifyPropertyChanged(nameof(Size));
        }
        public void UpdateMemberGear(S_SPAWN_USER sSpawnUser)
        {
            var u = Members.FirstOrDefault(x => x.PlayerId == sSpawnUser.PlayerId && x.ServerId == sSpawnUser.ServerId);
            if (u == null) return;
            u.Weapon = sSpawnUser.Weapon;
            u.Armor = sSpawnUser.Armor;
            u.Gloves = sSpawnUser.Gloves;
            u.Boots = sSpawnUser.Boots;
        }
        public void UpdateMyGear()
        {
            var u = Members.FirstOrDefault(x => x.IsPlayer);
            if (u == null) return;

            u.Weapon = InfoWindowViewModel.Instance.CurrentCharacter.Gear.FirstOrDefault(x => x.Piece == GearPiece.Weapon);
            u.Armor = InfoWindowViewModel.Instance.CurrentCharacter.Gear.FirstOrDefault(x => x.Piece == GearPiece.Armor);
            u.Gloves = InfoWindowViewModel.Instance.CurrentCharacter.Gear.FirstOrDefault(x => x.Piece == GearPiece.Hands);
            u.Boots = InfoWindowViewModel.Instance.CurrentCharacter.Gear.FirstOrDefault(x => x.Piece == GearPiece.Feet);

        }
        public void UpdateMemberLocation(S_PARTY_MEMBER_INTERVAL_POS_UPDATE p)
        {
            var u = Members.FirstOrDefault(x => x.PlayerId == p.PlayerId && x.ServerId == p.ServerId);
            if(u == null) return;
            var ch = p.Channel > 1000 ? "" : " ch." + p.Channel;
            u.Location = MapDatabase.TryGetGuardOrDungeonNameFromContinentId(p.ContinentId, out var l) ? l + ch : "Unknown";
        }

    }
}
