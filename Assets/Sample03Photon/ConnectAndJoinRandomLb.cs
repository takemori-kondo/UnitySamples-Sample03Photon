// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ConnectAndJoinRandomLb.cs" company="Exit Games GmbH"/>
// <summary>Prototyping / sample code for Photon Realtime.</summary>
// <author>developer@exitgames.com</author>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Photon.Realtime.Demo
{
    public class ConnectAndJoinRandomLb : MonoBehaviour, IConnectionCallbacks, IMatchmakingCallbacks, ILobbyCallbacks, IOnEventCallback
    {
        [SerializeField]
        protected AppSettings appSettings = new AppSettings();
        protected LoadBalancingClient lbc;

        protected ConnectionHandler ch;
        [SerializeField] protected Text StateUiText;
        [SerializeField] protected Button btnJoin;
        protected bool btnJoinIsPressed = false;
        public const string CHARACTER = "character";
        [SerializeField] protected Button btnChara1;
        [SerializeField] protected Button btnChara2;
        [SerializeField] protected Button btnChara3;
        [SerializeField] protected Button btnChara4;
        protected const int EVENT_CODE_DEBUG = 0;
        [SerializeField] protected Button btnOpRaiseEvent;
        protected float delta_sec = 0f;
        protected const float INTERVAL_SEC = 0.8f;

        protected virtual void Start()
        {
            this.lbc = new LoadBalancingClient();
            this.lbc.AddCallbackTarget(this);

            if (!this.lbc.ConnectUsingSettings(appSettings))
            {
                Debug.LogError("Error while connecting");
            }

            this.ch = this.gameObject.GetComponent<ConnectionHandler>();
            if (this.ch != null)
            {
                this.ch.Client = this.lbc;
                this.ch.StartFallbackSendAckThread();
            }

            this.btnJoin?.onClick.AddListener(() => OnBtnJoinClick());
            this.btnChara1?.onClick.AddListener(() => OnBtnCharaClick(this.btnChara1));
            this.btnChara2?.onClick.AddListener(() => OnBtnCharaClick(this.btnChara2));
            this.btnChara3?.onClick.AddListener(() => OnBtnCharaClick(this.btnChara3));
            this.btnChara4?.onClick.AddListener(() => OnBtnCharaClick(this.btnChara4));
            this.btnOpRaiseEvent?.onClick.AddListener(() => OnBtnOpRaiseEventClick());
        }

        protected virtual void Update()
        {
            LoadBalancingClient client = this.lbc;
            if (client != null)
            {
                this.delta_sec += Time.deltaTime;
                if (INTERVAL_SEC <= this.delta_sec)
                {
                    this.delta_sec = 0f;
                    client.Service();
                }


                Text uiText = this.StateUiText;
                string state = client.State.ToString();
                if (uiText != null && !uiText.text.Equals(state))
                {
                    uiText.text = this.GetStateText(client, state);
                }
            }
        }

        protected string GetStateText(LoadBalancingClient client, string state)
        {
            var selectedChara = client.LocalPlayer.CustomProperties[CHARACTER] ?? "";
            var currentRoom = client.CurrentRoom;
            var roomMembers = "";
            if (currentRoom != null)
            {
                foreach (var kvp in currentRoom.Players)
                {
                    var player = kvp.Value;
                    string memberChara = player.CustomProperties[CHARACTER]?.ToString();
                    if (!string.IsNullOrEmpty(memberChara))
                    {
                        roomMembers += memberChara + " ";
                    }
                }
                roomMembers.Trim();
            }
            return "State: " + state + "\n"
                + "Current Room: " + (currentRoom?.Name ?? "") + "\n"
                + "Selected Chara: " + selectedChara + "\n"
                + "Room Members: " + roomMembers;
        }

        #region IConnectionCallbacks ###########################################################

        public void OnConnected()
        {
        }

        public virtual void OnConnectedToMaster()
        {
            Debug.Log("OnConnectedToMaster");
        }

        public void OnDisconnected(DisconnectCause cause)
        {
            Debug.Log("OnDisconnected(" + cause + ")");
        }

        public void OnCustomAuthenticationResponse(Dictionary<string, object> data)
        {
        }

        public void OnCustomAuthenticationFailed(string debugMessage)
        {
        }

        public void OnRegionListReceived(RegionHandler regionHandler)
        {
            Debug.Log("OnRegionListReceived");
            regionHandler.PingMinimumOfRegions(this.OnRegionPingCompleted, null);
        }

        /// <summary>A callback of the RegionHandler, provided in OnRegionListReceived.</summary>
        /// <param name="regionHandler">The regionHandler wraps up best region and other region relevant info.</param>
        protected void OnRegionPingCompleted(RegionHandler regionHandler)
        {
            Debug.Log("OnRegionPingCompleted " + regionHandler.BestRegion);
            Debug.Log("RegionPingSummary: " + regionHandler.SummaryToCache);
            this.lbc.ConnectToRegionMaster(regionHandler.BestRegion.Code);
        }

        #endregion

        #region ILobbyCallbacks ################################################################

        public void OnRoomListUpdate(List<RoomInfo> roomList)
        {
        }

        public void OnLobbyStatisticsUpdate(List<TypedLobbyInfo> lobbyStatistics)
        {
        }

        public void OnJoinedLobby()
        {
        }

        public void OnLeftLobby()
        {
        }

        #endregion

        #region IMatchmakingCallbacks ##########################################################

        public void OnFriendListUpdate(List<FriendInfo> friendList)
        {
        }

        public void OnCreatedRoom()
        {
        }

        public void OnCreateRoomFailed(short returnCode, string message)
        {
        }

        public virtual void OnJoinedRoom()
        {
            Debug.Log("OnJoinedRoom");
        }

        public void OnJoinRoomFailed(short returnCode, string message)
        {
        }

        public void OnJoinRandomFailed(short returnCode, string message)
        {
            Debug.Log("OnJoinRandomFailed");
            var param = new EnterRoomParams();
            // param.Lobby
            param.RoomName = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
            param.RoomOptions = new RoomOptions();
            param.RoomOptions.MaxPlayers = 4;
            this.lbc.OpCreateRoom(param);
        }

        public void OnLeftRoom()
        {
        }

        #endregion

        #region IOnEventCallback ###############################################################

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent.Code == EVENT_CODE_DEBUG)
            {
                var eventContent = photonEvent.Parameters?[ParameterCode.Data] as string;
                Debug.Log($"<color=#99FF99>OnEvent eventCode={photonEvent.Code}, eventContent={eventContent}</color>");
            }
            // https://doc.photonengine.com/pun/current/gameplay/rpcsandraiseevent
            if (photonEvent.Code < 200)
            {
                this.OnCustomEventReceived?.Invoke(this, photonEvent);
            }
        }

        #endregion

        protected void OnBtnJoinClick()
        {
            this.btnJoinIsPressed = !this.btnJoinIsPressed;
            var btnJoinText = this.btnJoin.GetComponentInChildren<TextMeshProUGUI>();
            if (this.btnJoinIsPressed)
            {
                btnJoinText.text = "Exit Room";
                this.lbc.OpJoinRandomRoom();    // joins any open room (no filter)
            }
            else
            {
                btnJoinText.text = "Join";
                this.lbc.OpLeaveRoom(false);
            }
        }

        protected void OnBtnCharaClick(Button button)
        {
            var hashtable = new Hashtable();
            hashtable[CHARACTER] =
                button == this.btnChara1 ? "chara1" :
                button == this.btnChara2 ? "chara2" :
                button == this.btnChara3 ? "chara3" :
                button == this.btnChara4 ? "chara4" :
                "unknown";
            this.lbc.LocalPlayer.SetCustomProperties(hashtable);
        }

        protected void OnBtnOpRaiseEventClick()
        {
            var selectedChara = this.lbc.LocalPlayer.CustomProperties[CHARACTER] ?? "unknown";
            var eventContent = $"{selectedChara} sent debug message!";
            this.OpRaiseEvent(EVENT_CODE_DEBUG, eventContent);
        }

        public event EventHandler<EventData> OnCustomEventReceived;
        public Dictionary<int, Player> Players { get { return this.lbc?.CurrentRoom?.Players; } }
        public Dictionary<int, Player> OtherPlayers
        {
            get
            {
                var allPlayers = this.lbc?.CurrentRoom?.Players;
                if (allPlayers == null) return null;
                var otherPlayers = new Dictionary<int, Player>();
                foreach (var kvp in allPlayers)
                {
                    var player = kvp.Value;
                    if (!player.IsLocal)
                    {
                        otherPlayers.Add(kvp.Key, player);
                    }
                }
                return otherPlayers;
            }
        }
        public Player LocalPlayer { get { return this.lbc?.LocalPlayer; } }
        public void OpRaiseEvent(byte eventCode, string eventContent)
        {
            Debug.Log($"<color=#99FF99>OpRaiseEvent, {eventCode}</color>");
            this.lbc.OpRaiseEvent(eventCode, eventContent, RaiseEventOptions.Default, SendOptions.SendReliable);
        }
    }
}