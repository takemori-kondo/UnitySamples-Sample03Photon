using ExitGames.Client.Photon;
using Photon.Realtime;
using Photon.Realtime.Demo;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

/*
 * 状態              |提案  |レディ|個別処理|命令
 * ------------------|------|------|--------|------
 * Free       自由   |受信  | -    | -      | -
 * Proposed   提案   |衝突  | -    | -      | -
 * Conflicted 衝突   |受信  | -    | -      | -
 * Received   受信   |受信  |準備中| -      | -
 * ------------------|------|------|--------|------
 * Ready      レディ | -    | -    | -      | -
 * Preparing  準備中 | -    | -    |準備済  | -
 * Prepared   準備済 | -    | -    | -      |自由
 * Command    命令   | -    | -    | -      | -
 * 
 * 1. Freeは提案できる
 * 2. Conflictedしたらランダムに待って再送。その前に相手が送信したら受信
 * 3. Proposed以外の全員がReceivedしたら、レディを送信する
 * 4. Preparingは外部はUpdateの引数で最終的にPreparedになる
 * 5. Ready以外の全員がPreparedしたら、Commandする
 */


public class FlowControlHelper
{
    public const string COMM_STATE = "FlowControlHelper_COMM_STATE";
    public enum CommState
    {
        Free = 0,
        Proposed = 10,
        Conflicted = 20,
        Received = 30,

        Ready = 40,
        Preparing = 50,
        Prepared = 60,
        Command = 70
    }

    public event EventHandler OnReadyReceived;
    public event EventHandler<string> OnCommandReceived;
    public event EventHandler<string> OnCommandSent;

    public readonly List<EventData> EventQueue = new List<EventData>();
    public ConnectAndJoinRandomLb Client { get; private set; }

    public string PayloadCache { get; private set; } = string.Empty;
    public readonly float ProposeWaitMax_sec;
    private float proposeCounter_sec = 0;
    public readonly float DummyWait_sec;
    public readonly float ConflictDelayRange_sec;
    private float conflictDelay_sec = 0;
    private float conflictCounter_sec = 0;

    public readonly int AdditionalCodeValue;
    private string PropKey { get { return COMM_STATE + this.AdditionalCodeValue.ToString(); } }
    private byte AddedCode(CommState commState) { return (byte)((int)commState + this.AdditionalCodeValue); }

    public FlowControlHelper(ConnectAndJoinRandomLb client, float proposeWaitMax_sec = 10.0f, float dummyWait_sec = 1.5f, float conflictDelayRange_sec = 3.0f, int additionalCodeValue = 0)
    {
        this.Client = client;
        if (this.Client != null)
        {
            this.Client.OnCustomEventReceived += (object sender, EventData photonEvent) =>
            {
                this.EventQueue.Add(photonEvent);
            };
        }

        this.ProposeWaitMax_sec = proposeWaitMax_sec;
        this.DummyWait_sec = dummyWait_sec;
        this.ConflictDelayRange_sec = conflictDelayRange_sec;

        if (additionalCodeValue < 0 || 10 <= additionalCodeValue) throw new ArgumentException($"additionalCodeValue must be 0-9");
        this.AdditionalCodeValue = additionalCodeValue;
    }

    private CommState _currentState = CommState.Free;
    public CommState CurrentState
    {
        get { return this._currentState; }
        private set
        {
            this._currentState = value;
            var hashtable = new Hashtable();
            hashtable[this.PropKey] = this.CurrentState;
            this.Client?.LocalPlayer?.SetCustomProperties(hashtable);
            switch (value)
            {
                case CommState.Free:
                    this.PayloadCache = String.Empty;
                    break;
                case CommState.Proposed:
                    this.proposeCounter_sec = 0;
                    break;
                case CommState.Conflicted:
                    this.conflictDelay_sec = Random.Range(0.0f, this.ConflictDelayRange_sec);
                    this.conflictCounter_sec = 0.0f;
                    break;
                case CommState.Received:
                    break;
                case CommState.Ready:
                    break;
                case CommState.Preparing:
                    break;
                case CommState.Prepared:
                    break;
                case CommState.Command:
                    break;
                default:
                    break;
            }
            Debug.Log($"{nameof(this.CurrentState)}={this.CurrentState}, {nameof(this.AdditionalCodeValue)}={this.AdditionalCodeValue}");
        }
    }

    public void Update()
    {
        this.conflictCounter_sec += Time.deltaTime;
        this.proposeCounter_sec += Time.deltaTime;
        var proposed = this.DequeueOrDefault(CommState.Proposed);
        var ready___ = this.DequeueOrDefault(CommState.Ready);
        var command_ = this.DequeueOrDefault(CommState.Command);

        // Proposedに対する状態遷移
        if (proposed != null)
        {
            switch (this.CurrentState)
            {
                case CommState.Free:
                case CommState.Conflicted:
                    this.CurrentState = CommState.Received;
                    break;
                case CommState.Proposed:
                    this.CurrentState = CommState.Conflicted;
                    break;
                default:
                    break;
            }
        }

        // Readyに対する状態遷移   
        if (ready___ != null)
        {
            switch (this.CurrentState)
            {
                case CommState.Received:
                    this.OnReadyReceived?.Invoke(this, EventArgs.Empty);
                    this.CurrentState = CommState.Preparing;
                    break;
                default:
                    break;
            }
        }

        // Commandに対する状態遷移
        if (command_ != null)
        {
            var eventContent = command_.Parameters?[ParameterCode.Data] as string;
            this.OnCommandReceived?.Invoke(this, eventContent);
            this.CurrentState = CommState.Free;
        }

        // 全てReceivedになってしまった場合、PayloadCache持ちはConflict扱いに戻す
        if (this.CurrentState == CommState.Received)
        {
            if (this.OtherPlayersAre(new[] { CommState.Received }) &&
                !string.IsNullOrWhiteSpace(this.PayloadCache))
            {
                this.CurrentState = CommState.Conflicted;
            }
        }

        // Conflicted状態：判断にPhoton値を参照するため、値が遅れてることの考慮が必要
        if (this.CurrentState == CommState.Conflicted)
        {
            if (this.OtherPlayersAre(new[] { CommState.Free, CommState.Proposed, CommState.Conflicted, CommState.Received }) &&
                this.conflictDelay_sec <= this.conflictCounter_sec)
            {
                this.SendEvent(CommState.Proposed);
            }
        }

        // Proposed状態：判断にPhoton値を参照するため、値が遅れてることの考慮が必要
        if (this.CurrentState == CommState.Proposed)
        {
            if (this.OtherPlayersAre(new[] { CommState.Received }, true) || this.ProposeWaitMax_sec <= this.proposeCounter_sec)
            {
                this.SendEvent(CommState.Ready);
            }
        }

        // Ready状態：判断にPhoton値を参照するため、値が遅れてることの考慮が必要
        if (this.CurrentState == CommState.Ready)
        {
            if (this.OtherPlayersAre(new[] { CommState.Prepared }) || this.ProposeWaitMax_sec <= this.proposeCounter_sec)
            {
                this.SendEvent(CommState.Command);
                this.OnCommandSent?.Invoke(this, this.PayloadCache);
                this.CurrentState = CommState.Free;
            }
        }
    }

    public bool SendProposeMessage(string payload)
    {
        if (this.CurrentState != CommState.Free && this.CurrentState != CommState.Conflicted) return false;

        this.PayloadCache = payload;
        if (!this.OtherPlayersAre(new[] { CommState.Free, CommState.Proposed, CommState.Conflicted, CommState.Received }))
        {
            this.CurrentState = CommState.Conflicted; // 予約として転用
            return false;
        }
        this.SendEvent(CommState.Proposed);
        return true;
    }

    public bool Prepared()
    {
        if (this.CurrentState != CommState.Preparing) return false;

        this.CurrentState = CommState.Prepared;
        return true;
    }

    public string GetDebugText()
    {
        var sortedPlayers = this.Client?.Players?.OrderBy(kvp => kvp.Key);
        var text = $"{nameof(this.Client)}={this.Client}\n";
        text += $"{nameof(this.PayloadCache)}={this.PayloadCache}\n";
        text += $"{nameof(this.PropKey)}={this.PropKey}, {nameof(this.CurrentState)}={this.CurrentState}\n";
        if (sortedPlayers != null)
        {
            foreach (var kvp in sortedPlayers)
            {
                var player = kvp.Value;
                var commState = (CommState)(player?.CustomProperties[this.PropKey] ?? CommState.Free);
                text += $"p={kvp.Key}, commState={commState}\n";
            }
        }
        return text;
    }


    //-------------------------


    private EventData DequeueOrDefault(CommState commState)
    {
        var result = this.EventQueue
             .Where(eventData => eventData.Code == AddedCode(commState))
             .FirstOrDefault();
        this.EventQueue.Remove(result);
        return result;
    }

    private void SendEvent(CommState commState)
    {
        this.Client?.OpRaiseEvent(AddedCode(commState), this.PayloadCache);
        this.CurrentState = commState;
    }

    private bool OtherPlayersAre(CommState[] expectedStates, bool doesDummyWait = false)
    {
        var otherPlayers = this.Client?.OtherPlayers;
        if (otherPlayers != null)
        {
            foreach (var kvp in otherPlayers)
            {
                var player = kvp.Value;
                var commState = (CommState)(player?.CustomProperties[this.PropKey] ?? CommState.Free);
                if (!expectedStates.Contains(commState))
                {
                    return false;
                }
            }
        }
        else
        {
            if (doesDummyWait)
            {
                if (this.proposeCounter_sec < this.DummyWait_sec)
                {
                    return false;
                }
                Debug.Log("DummyWait End");
            }
        }
        return true;
    }
}
