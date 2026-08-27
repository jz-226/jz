using System;
using System.Threading.Tasks;
using UnityEngine;
using TapSDK.Core;
using TapSDK.Login;

namespace Before8AM.TapTap
{
    /// <summary>
    /// [TapTap 上架] TapTap 登录桥：接入 TapTap V4 SDK（TapSDK.Core + TapSDK.Login）。
    /// TapPlay 包体强制要求「包体内正确接入 TapTap 登录」——玩家以 TapTap 账号身份进入游戏。
    /// Client ID / Client Token 来自 TapTap 开发者后台 → 游戏服务 → 应用配置。
    /// 启动静默恢复：GetCurrentTapAccount 恢复上次登录；未登录时主菜单显示登录按钮。
    /// </summary>
    public static class TapTapLoginBridge
    {
        // Public-source placeholder: configure these values in a local, untracked file before building.
        const string ClientId = "YOUR_TAPTAP_CLIENT_ID";
        const string ClientToken = "YOUR_TAPTAP_CLIENT_TOKEN";

        static bool initialized;
        static TapTapAccount current;
        static bool restoring;

        public static bool IsInitialized => initialized;
        public static bool IsLoggedIn => current != null;
        public static bool IsBusy => restoring;

        /// <summary>显示用昵称：有昵称用昵称，否则退回 unionId（玩家不感知，仅 UI 展示）。</summary>
        public static string DisplayName
        {
            get
            {
                if (current == null) return "";
                return string.IsNullOrEmpty(current.name) ? current.unionId : current.name;
            }
        }

        /// <summary>[TapTap 合规] 用户在隐私政策弹窗点「同意」后调用一次：初始化 SDK + 静默恢复上次登录。
        /// 绝不早于用户同意前初始化（TapTap 审核要求）。任何异常都不影响游戏本体。</summary>
        public static void Init()
        {
            if (initialized) return;
            initialized = true;
            try
            {
                var core = new TapTapSdkOptions
                {
                    clientId = ClientId,
                    clientToken = ClientToken,
                    region = TapTapRegionType.CN,
                    screenOrientation = 1,   // 0=竖屏 1=横屏（本游戏横屏）
                    enableLog = true,        // 开发期排查用，正式上架前改 false
                };
                TapTapSDK.Init(core);
                RestoreAccountAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TapTap] SDK 初始化失败：{e.Message}（不影响离线游戏）");
            }
        }

        /// <summary>发起 TapTap 登录授权，成功返回 true。玩家取消/失败返回 false。</summary>
        public static async Task<bool> LoginAsync()
        {
            if (!initialized) return false;
            try
            {
                var account = await TapTapLogin.Instance.LoginWithScopes(new[] { TapTapLogin.TAP_LOGIN_SCOPE_PUBLIC_PROFILE });
                current = account;
                return current != null;
            }
            catch (TaskCanceledException)
            {
                Debug.Log("[TapTap] 玩家取消登录");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TapTap] 登录失败：{e.Message}");
                return false;
            }
        }

        /// <summary>登出：清除本地缓存账号。</summary>
        public static void Logout()
        {
            try
            {
                TapTapLogin.Instance.Logout();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TapTap] 登出失败：{e.Message}");
            }
            current = null;
        }

        static async void RestoreAccountAsync()
        {
            restoring = true;
            try
            {
                current = await TapTapLogin.Instance.GetCurrentTapAccount();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TapTap] 恢复登录态失败：{e.Message}");
                current = null;
            }
            finally
            {
                restoring = false;
            }
        }
    }
}
