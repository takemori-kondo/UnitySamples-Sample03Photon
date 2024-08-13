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
        private AppSettings appSettings = new AppSettings();
        private LoadBalancingClient lbc;

        private ConnectionHandler ch;
        [SerializeField] private Text StateUiText;
        [SerializeField] private Button btnJoin;
        private bool btnJoinIsPressed = false;
        private const string CHARACTER = "character";
        [SerializeField] private Button btnChara1;
        [SerializeField] private Button btnChara2;
        [SerializeField] private Button btnChara3;
        [SerializeField] private Button btnChara4;
        private const int EVENT_CODE_DEBUG = 0;
        [SerializeField] private Button btnOpRaiseEvent;

        public void Start()
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

        public void Update()
        {
            LoadBalancingClient client = this.lbc;
            if (client != null)
            {
                client.Service();


                Text uiText = this.StateUiText;
                string state = client.State.ToString();
                if (uiText != null && !uiText.text.Equals(state))
                {
                    uiText.text = this.GetStateText(client, state);
                }
            }
        }

        private string GetStateText(LoadBalancingClient client, string state)
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

        public void OnConnectedToMaster()
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
        private void OnRegionPingCompleted(RegionHandler regionHandler)
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

        public void OnJoinedRoom()
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
        }

        #endregion

        private void OnBtnJoinClick()
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

        private void OnBtnCharaClick(Button button)
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

        private void OnBtnOpRaiseEventClick()
        {
            var selectedChara = this.lbc.LocalPlayer.CustomProperties[CHARACTER] ?? "unknown";
            var eventContent = $"{selectedChara} sent debug message!";
            this.lbc.OpRaiseEvent(EVENT_CODE_DEBUG, eventContent, RaiseEventOptions.Default, SendOptions.SendReliable);
            Debug.Log("<color=#99FF99>OpRaiseEvent</color>");
        }
    }
}