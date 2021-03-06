﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TCC.Data;
using TCC.Parsing;
using TCC.ViewModels;

namespace TCC
{
    public static class Proxy
    {
        private static TcpClient _client = new TcpClient();
        private static int _retries = 2;

        public static bool IsConnected => _client.Connected;

        public static void ConnectToProxy()
        {
            try
            {
                if (_client.Client != null && _client.Connected) return;
                Debug.WriteLine("Connecting...");
                ChatWindowViewModel.Instance.AddTccMessage("Trying to connect to tera-proxy...");

                _client = new TcpClient();
                _client.Connect("127.0.0.50", 9550);
                Debug.WriteLine("Connected");
                ChatWindowViewModel.Instance.AddTccMessage("Connected to tera-proxy.");
                var t = new Thread(ReceiveData);
                t.Start();
            }
            catch (Exception e)
            {
                _retries--;
                Debug.WriteLine(e.Message);
                Task.Delay(2000).ContinueWith(t =>
                {
                    if (_retries <= 0)
                    {
                        Debug.WriteLine("Maximum retries exceeded...");
                        ChatWindowViewModel.Instance.AddTccMessage("Maximum retries exceeded. tera-proxy functionalities won't be available.");

                        _retries = 2;
                        return;
                    }
                    ConnectToProxy();
                });
            }
        }
        private static void SendData(string data)
        {
            try
            {
                var stream = _client.GetStream();
                UTF8Encoding utf8 = new UTF8Encoding();
                byte[] bb = utf8.GetBytes(data);
                stream.Write(bb, 0, bb.Length);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                _retries = 2;
                //ConnectToProxy();
            }
        }
        private static void ReceiveData()
        {
            var buffer = new byte[2048];
            try
            {
                while (_client.Connected)
                {
                    var size = _client.GetStream().Read(buffer, 0, buffer.Length);
                    var msg = "<FONT>"+Encoding.UTF8.GetString(buffer.Take(size).ToArray())+"</FONT>";
                    PacketProcessor.HandleCommandOutput(msg);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        public static void CloseConnection()
        {
            try
            {
                _client?.GetStream().Close();
                _client?.Client?.Close();
                _client?.Close();
            }
            catch
            {
                // ignored
            }
        }

        public static void ChatLinkData(string linkData)
        {
            // linkData = T#####DATA
            if (linkData == null) return;
            var sb = new StringBuilder("chat_link");
            sb.Append("&");
            sb.Append(":tcc:");
            sb.Append(linkData.Replace("#####", ":tcc:"));
            sb.Append(":tcc:");
            System.IO.File.AppendAllText("link-test.txt", sb + "\n");
            SendData(sb.ToString());
        }
        public static void RequestPartyInfo(int id)
        {
            var sb = new StringBuilder("lfg_party_req");
            sb.Append("&id=");
            sb.Append(id);

            SendData(sb.ToString());
        }
        public static void FriendRequest(string name, string message)
        {
            var sb = new StringBuilder("friend_req");
            sb.Append("&name=");
            sb.Append(name);
            sb.Append("&msg=");
            sb.Append(message);

            SendData(sb.ToString());
        }
        public static void UnfriendUser(string name)
        {
            var sb = new StringBuilder("unfriend");
            sb.Append("&name=");
            sb.Append(name);

            SendData(sb.ToString());
        }
        public static void BlockUser(string name)
        {
            var sb = new StringBuilder("block");
            sb.Append("&name=");
            sb.Append(name);

            SendData(sb.ToString());
        }
        public static void SetInvitePower(uint serverId, uint playerId, bool v)
        {
            var sb = new StringBuilder("power_change");
            sb.Append("&sId=");
            sb.Append(serverId);
            sb.Append("&pId=");
            sb.Append(playerId);
            sb.Append("&power=");
            sb.Append(v ? 1 : 0);

            SendData(sb.ToString());
        }
        public static void UnblockUser(string name)
        {
            var sb = new StringBuilder("unblock");
            sb.Append("&name=");
            sb.Append(name);

            SendData(sb.ToString());
        }
        public static void AskInteractive(uint srvId, string name)
        {
            var sb = new StringBuilder("ask_int");
            sb.Append("&srvId=");
            sb.Append(srvId);
            sb.Append("&name=");
            sb.Append(name);

            SendData(sb.ToString());
        }
        public static void DelegateLeader(uint serverId, uint playerId)
        {
            var sb = new StringBuilder("leader");
            sb.Append("&sId=");
            sb.Append(serverId);
            sb.Append("&pId=");
            sb.Append(playerId);

            SendData(sb.ToString());
        }
        public static void Inspect(string name)
        {
            var sb = new StringBuilder("inspect");
            sb.Append("&name=");
            sb.Append(name);

            SendData(sb.ToString());
        }
        public static void PartyInvite(string name)
        {
            var sb = new StringBuilder("inv_party");
            sb.Append("&name=");
            sb.Append(name);
            sb.Append("&raid=");
            sb.Append(GroupWindowViewModel.Instance.Raid ? 1 : 0);

            SendData(sb.ToString());
        }
        public static void GuildInvite(string name)
        {
            var sb = new StringBuilder("inv_guild");
            sb.Append("&name=");
            sb.Append(name);

            SendData(sb.ToString());
        }
        public static void DeclineBrokerOffer(uint playerId, uint listingId)
        {
            var sb = new StringBuilder("tb_decline");
            sb.Append("&player=");
            sb.Append(playerId);
            sb.Append("&listing=");
            sb.Append(listingId);

            SendData(sb.ToString());

        }
        public static void AcceptBrokerOffer(uint playerId, uint listingId)
        {
            var sb = new StringBuilder("tb_accept");
            sb.Append("&player=");
            sb.Append(playerId);
            sb.Append("&listing=");
            sb.Append(listingId);

            SendData(sb.ToString());

        }
        public static void DeclineApply(uint playerId)
        {
            var sb = new StringBuilder("apply_decline");
            sb.Append("&player=");
            sb.Append(playerId);

            SendData(sb.ToString());
        }
        public static void ExTooltip(long itemUid, string ownerName)
        {
            var sb = new StringBuilder("ex_tooltip");
            sb.Append("&uid=");
            sb.Append(itemUid);
            sb.Append("&name=");
            sb.Append(ownerName);

            SendData(sb.ToString());
        }
        public static void NondbItemInfo(uint itemId)
        {
            var sb = new StringBuilder("nondb_info");
            sb.Append("&id=");
            sb.Append(itemId);

            SendData(sb.ToString());
        }
    }
}
