/*
┌────────────────────────────┐
│　Description: 热更新面板
│　Remark: 
└────────────────────────────┘
*/
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Lin.Runtime.Helper;
using Lin.Runtime.Resource.Updater;
using System.ComponentModel;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting;

namespace Lin.Runtime.UI
{
    [Preserve]
    public partial class UpdatePanel : PanelBase
    {
        private enum EState
        {
            [Description("正在检测更新")]
            CHECKING,

            [Description("检测到新版本")]
            SHOULD_UPDATE,

            [Description("正在下载资源")]
            UPDATING,

            [Description("部分资源下载失败, 请稍后重试")]
            ERROR,

            [Description("资源更新完成")]
            COMPLATED,

            [Description("资源已是最新版本")]
            NEWEST,
        }

        protected override void OnShow()
        {
            progressImage.fillAmount = 0;
            progressTextMeshProUGUI.text = string.Empty;

            SetVersion("App", Application.version, "-", appVersionTextMeshProUGUI);
            if (GlobalConfig_SO.GetInstance().isStandaloneMode)
                resVersionTextMeshProUGUI.gameObject.SetActive(false);
            else
                SetVersion("Res", "-", "-", resVersionTextMeshProUGUI);

            ChangeState(EState.CHECKING);
        }

        private void ChangeState(EState state)
        {
            infosWindowRectTransform.gameObject.SetActive(state == EState.SHOULD_UPDATE);
            noticeTextMeshProUGUI.gameObject.SetActive(state != EState.SHOULD_UPDATE);
            infosWindowButton.gameObject.SetActive(state == EState.UPDATING);
            confirmButton.gameObject.SetActive(state == EState.SHOULD_UPDATE);
            cancelButton.gameObject.SetActive(state == EState.SHOULD_UPDATE);
            progressTextMeshProUGUI.gameObject.SetActive(state == EState.UPDATING);

            noticeTextMeshProUGUI.text = state.GetDescription();
        }

        private void SetVersion(string title, string local, string remote, TMP_Text text)
        {
            if (GlobalConfig_SO.GetInstance().isStandaloneMode)
                text.text = $"{title} LocalVersion: {local}";
            else
                text.text = $"{title} LocalVersion: {local}\n{title} RemoteVersion: {remote}";
        }

        public void SetResVersion(string local, string remote) => SetVersion("Res", local, remote, resVersionTextMeshProUGUI);

        public void SetAppVersion(string local, string remote) => SetVersion("App", local, remote, appVersionTextMeshProUGUI);

#if UNITY_ANDROID
        public async UniTask ShowAndroidInfosAsync(AndroidUpdater updater)
        {
            var descriptions = await updater.GetVersionDescriptions();

            ChangeState(EState.SHOULD_UPDATE);

            confirmButton.RemoveAllClickListeners();
            confirmButton.AddOnClickListener(OnConfirm);
            updateInfosTextMeshProUGUI.text = descriptions.GetDescriptions();

            void OnConfirm()
            {
                updater.BeginDownload().Forget();
                ChangeState(EState.UPDATING);
            }
        }

        public void OnUpdateAndroidStart()
        {
            noticeTextMeshProUGUI.text = "正在下载安装包";
            progressTextMeshProUGUI.enabled = true;
            sizeTextMeshProUGUI.enabled = true;
        }

        public void OnUpdateApkFinished(bool @do, bool isSucceeded)
        {
            if (@do)
                ChangeState(isSucceeded ? EState.COMPLATED : EState.ERROR);
            else
                ChangeState(EState.CHECKING);
        }
#elif UNITY_IOS

#endif

        public void RefreshProgress(float progress, long totalDownloadBytes)
        {
            progressImage.fillAmount = progress;
            progressTextMeshProUGUI.text = $"{(progress * 100):0} %";

            float d = IOHelper.B2MB((long)(progress * totalDownloadBytes));
            float t = IOHelper.B2MB(totalDownloadBytes);
            sizeTextMeshProUGUI.text = $"已下载: {d.ToString("0.00")}MB, 资源大小: {t.ToString("0.00")}MB";
        }

        public async UniTask ShowResourceInfosAsync(ResourcesUpdater updater)
        {
            var descriptions = await updater.GetVersionDescriptions();
            ChangeState(EState.SHOULD_UPDATE);

            confirmButton.RemoveAllClickListeners();
            confirmButton.AddOnClickListener(OnConfirm);

            using var sb = ZString.CreateStringBuilder();
            for (int i = 0; i < descriptions.Length; i++)
            {
                sb.Append(i + 1);
                sb.Append('.');
                sb.Append(descriptions[i]);
                if (i < descriptions.Length - 1)
                    sb.AppendLine();
            }
            updateInfosTextMeshProUGUI.text = sb.ToString();

            void OnConfirm()
            {
                updater.BeginDownload().Forget();
                ChangeState(EState.UPDATING);
            }
        }

        public void OnUpdatePackagesFinished(bool updated, bool succeeded)
        {
            if (updated)
                noticeTextMeshProUGUI.text = succeeded ? "资源更新完成, 正在进入游戏" : "资源更新失败, 请稍后重试";
            else
            {
                noticeTextMeshProUGUI.text = "资源加载完成, 正在进入游戏";
                progressImage.fillAmount = 1;
            }
            ChangeState(EState.NEWEST);
        }

        public void OnUpdatePackagesStart()
        {
            noticeTextMeshProUGUI.text = "正在更新资源";
            progressTextMeshProUGUI.enabled = true;
            sizeTextMeshProUGUI.enabled = true;
        }

        #region - Callback -

        private void RegisterCustomEvents() { }

        private void DeregisterCustomEvents() { }

        private void OnConfirmButtonClick() { }

        private void OnCancelButtonClick()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnInfosWindowButtonClick() => infosWindowRectTransform.gameObject.Toggle();

        #endregion
    }
}