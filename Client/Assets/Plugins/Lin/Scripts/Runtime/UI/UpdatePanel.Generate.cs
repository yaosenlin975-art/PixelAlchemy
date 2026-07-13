//CreateTime: 2026/6/15 11:23:14
using UnityEngine;
using Lin.Runtime.UI;
using UnityEngine.UI;
using Lin.Runtime.Helper;
using Sirenix.OdinInspector;
using TMPro;

namespace Lin.Runtime.UI
{
    //Description: 不要在这个脚本里进行逻辑编写, 在 Assets\Plugins\Lin\Scripts\Runtime\UI\UpdatePanel.cs 中编写具体逻辑
    public partial class UpdatePanel
    {
        [SerializeField, BoxGroup(CONTROLS_NAME)] private TextMeshProUGUI appVersionTextMeshProUGUI;
        public TextMeshProUGUI AppVersionTextMeshProUGUI => appVersionTextMeshProUGUI;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private TextMeshProUGUI resVersionTextMeshProUGUI;
        public TextMeshProUGUI ResVersionTextMeshProUGUI => resVersionTextMeshProUGUI;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private Button infosWindowButton;
        public Button InfosWindowButton => infosWindowButton;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private RectTransform infosWindowRectTransform;
        public RectTransform InfosWindowRectTransform => infosWindowRectTransform;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private TextMeshProUGUI updateInfosTextMeshProUGUI;
        public TextMeshProUGUI UpdateInfosTextMeshProUGUI => updateInfosTextMeshProUGUI;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private Button confirmButton;
        public Button ConfirmButton => confirmButton;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private Button cancelButton;
        public Button CancelButton => cancelButton;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private Image progressImage;
        public Image ProgressImage => progressImage;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private TextMeshProUGUI progressTextMeshProUGUI;
        public TextMeshProUGUI ProgressTextMeshProUGUI => progressTextMeshProUGUI;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private TextMeshProUGUI sizeTextMeshProUGUI;
        public TextMeshProUGUI SizeTextMeshProUGUI => sizeTextMeshProUGUI;
        [SerializeField, BoxGroup(CONTROLS_NAME)] private TextMeshProUGUI noticeTextMeshProUGUI;
        public TextMeshProUGUI NoticeTextMeshProUGUI => noticeTextMeshProUGUI;
        private bool reigsteredControlsListener;

        private void Reset()
        {
            appVersionTextMeshProUGUI = gameObject.GetComponentInChildren<TextMeshProUGUI>("[TextMeshProUGUI] AppVersion");
            resVersionTextMeshProUGUI = gameObject.GetComponentInChildren<TextMeshProUGUI>("[TextMeshProUGUI] ResVersion");
            infosWindowButton = gameObject.GetComponentInChildren<Button>("[Button] InfosWindow");
            infosWindowRectTransform = gameObject.GetComponentInChildren<RectTransform>("[RectTransform] InfosWindow");
            updateInfosTextMeshProUGUI = gameObject.GetComponentInChildren<TextMeshProUGUI>("[TextMeshProUGUI] UpdateInfos");
            confirmButton = gameObject.GetComponentInChildren<Button>("[Button] Confirm");
            cancelButton = gameObject.GetComponentInChildren<Button>("[Button] Cancel");
            progressImage = gameObject.GetComponentInChildren<Image>("[Image] Progress");
            progressTextMeshProUGUI = gameObject.GetComponentInChildren<TextMeshProUGUI>("[TextMeshProUGUI] Progress");
            sizeTextMeshProUGUI = gameObject.GetComponentInChildren<TextMeshProUGUI>("[TextMeshProUGUI] Size");
            noticeTextMeshProUGUI = gameObject.GetComponentInChildren<TextMeshProUGUI>("[TextMeshProUGUI] Notice");
        }

        public override void RegisterEvents()
        {
            RegisterCustomEvents();
            // 组件固定监听, 不在Deregister中移除, 避免组件重复注册监听
            if(reigsteredControlsListener)
                return;

            // Buttons OnClick
            infosWindowButton.AddOnClickListener(OnInfosWindowButtonClick);
            confirmButton.AddOnClickListener(OnConfirmButtonClick);
            cancelButton.AddOnClickListener(OnCancelButtonClick);
            reigsteredControlsListener = true;
        }
        public override void DeregisterEvents()
        {
            DeregisterCustomEvents();
        }

    }
}
