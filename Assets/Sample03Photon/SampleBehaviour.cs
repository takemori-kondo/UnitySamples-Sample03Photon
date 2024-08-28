using Photon.Realtime.Demo;
using UnityEngine;
using UnityEngine.UI;

public class SampleBehaviour : MonoBehaviour
{
    [SerializeField] private ConnectAndJoinRandomLb connectAndJoinRandomLb;
    [SerializeField] private Button btnPropose;
    [SerializeField] private Button btnPrepared;
    [SerializeField] private GameObject debug;
    private FlowControlHelper flowControlHelper = null;

    // Start is called before the first frame update
    private void Start()
    {
        this.btnPropose.onClick.AddListener(OnBtnProposeClick);
        this.btnPrepared.onClick.AddListener(OnBtnPreparedClick);

        this.flowControlHelper = new FlowControlHelper(this.connectAndJoinRandomLb, 15, 1.5f, 5, 0);
        this.flowControlHelper.OnCommandReceived += FlowControlHelper_OnCommandReceived;
        this.flowControlHelper.OnCommandSent += FlowControlHelper_OnCommandSent;
    }

    // Update is called once per frame
    private void Update()
    {
        this.flowControlHelper.Update();
        if (Input.GetKeyDown(KeyCode.F12))
        {
            var reversedActive = this.debug?.gameObject?.activeSelf ?? true;
            this.debug?.gameObject.SetActive(!reversedActive);
        }
        var txt = this.debug.GetComponentInChildren<Text>();
        if (txt != null)
        {
            txt.text = this.flowControlHelper.GetDebugText();
        }
    }

    private void OnBtnProposeClick()
    {
        this.flowControlHelper.SendProposeMessage("this is payload!!!!");
    }

    private void OnBtnPreparedClick()
    {
        this.flowControlHelper.Prepared();
    }

    private void FlowControlHelper_OnCommandReceived(object sender, string e)
    {
        Debug.Log($"_OnCommandReceived! payload={e}");
    }

    private void FlowControlHelper_OnCommandSent(object sender, string e)
    {
        Debug.Log($"OnCommandSent! payload={e}");
    }
}
