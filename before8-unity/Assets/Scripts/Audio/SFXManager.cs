using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Before8AM.Core;   // [0.8.9g] SceneNames（BGM 按场景选曲）

namespace Before8AM.Audio
{
    /// <summary>
    /// [0.8.9] 全局音效播放器：加载精选免版权素材（Kenney CC0，Assets/Audio/SFX/Resources）。
    /// 静态单例惰性自建 + DontDestroyOnLoad——主菜单/校园/超市任何场景调用即播，无需场景接线
    /// （全代码生成风格：音频资源走 Resources.Load，代码零手挂引用）。
    /// 音量：AudioListener.volume ← PlayerPrefs "Before8AM.MasterVolume"（设置面板滑块调节）。
    /// [0.8.9c] 常驻自挂 AudioListener：主相机的 listener 会被开场过场（WindowIntro）禁用，
    /// 导致过场/切场景间隙 "no audio listeners" 警告刷屏 + 音频丢失；本单例 DontDestroyOnLoad
    /// 永不失效（音效全部 2D PlayOneShot，监听位置无关）。场景相机上的旧 listener 运行时清掉防双监听。
    /// </summary>
    public class SFXManager : MonoBehaviour
    {
        static SFXManager inst;
        AudioSource sfx;
        AudioSource bgm;          // [0.8.9g] BGM 音源（loop + 淡入；场景切换自动选曲）
        string bgmName;           // 当前曲名（同名跳过，防重复触发）
        const float BgmVol = 0.35f;   // [0.8.9g] BGM 音量（轻柔背景，跟随 AudioListener.volume 主音量）
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        bool uiClickQueued;   // [0.8.9c] OnGUI 只置标记、Update 播放：不在 GUI 事件流里调音频 API

        /// <summary>全局单例（首次访问自建，跨场景常驻）。</summary>
        public static SFXManager Instance
        {
            get
            {
                if (inst == null)
                {
                    GameObject go = new GameObject("SFXManager");
                    inst = go.AddComponent<SFXManager>();
                    DontDestroyOnLoad(go);
                }
                return inst;
            }
        }

        /// <summary>[0.8.9] 场景加载前自建实例：主菜单场景没有任何调用方会先访问单例，
        /// 不预建的话 OnGUI 全局点击音就永远不会触发（实例不存在）。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void EnsureInstance()
        {
            _ = Instance;
        }

        void Awake()
        {
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            AudioClip[] all = Resources.LoadAll<AudioClip>("");   // 加载 Resources 目录全部音频（当前只放音效）
            foreach (AudioClip c in all)
                if (c != null) clips[c.name] = c;
            AudioListener.volume = PlayerPrefs.GetFloat("Before8AM.MasterVolume", 1f);

            // [0.8.9g] BGM 音源（循环，音量 0 起由 Update 淡入）
            bgm = gameObject.AddComponent<AudioSource>();
            bgm.loop = true;
            bgm.playOnAwake = false;
            bgm.volume = 0f;
            SelectBgm(BgmForScene(SceneManager.GetActiveScene().name));   // 首次进 Play 也按当前场景选曲

            // [0.8.9c] 常驻 listener：跨场景永不缺失（过场禁用主相机也不丢音频）。
            gameObject.AddComponent<AudioListener>();
            SceneManager.sceneLoaded += OnSceneLoaded;
            StripSceneListeners();
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>[0.8.9c] 每个场景加载后清掉场景相机上的旧 AudioListener（本单例常驻监听足够，避免双监听警告）。
        /// [0.8.9g] 顺带按场景切 BGM（主菜单 / 游戏场景各自选曲，音量 0 起淡入）。</summary>
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StripSceneListeners();
            SelectBgm(BgmForScene(scene.name));
        }

        /// <summary>[0.8.9g] 场景 → BGM 选曲：主菜单用更空灵的 menu 曲，两个游戏场景共用 campus 曲。</summary>
        static string BgmForScene(string sceneName) =>
            sceneName == SceneNames.MainMenu ? "bgm_menu" : "bgm_campus";

        /// <summary>[0.8.9g] 切 BGM（同名跳过；clip 换入后从 0 音量淡入，Update 里渐变）。</summary>
        void SelectBgm(string name)
        {
            if (name == bgmName) return;
            bgmName = name;
            if (clips.TryGetValue(name, out AudioClip c))
            {
                bgm.clip = c;
                bgm.volume = 0f;
                bgm.Play();
            }
        }

        void StripSceneListeners()
        {
            foreach (var al in FindObjectsOfType<AudioListener>())
                if (al.gameObject != gameObject)
                    Destroy(al);
        }

        void Update()
        {
            // [0.8.9c] 点击音离开 GUI 事件流：OnGUI 置位、这里播放，杜绝音频调用对 IMGUI 状态机的任何干扰
            if (uiClickQueued)
            {
                uiClickQueued = false;
                PlayUiClick();
            }

            // [0.8.9g] BGM 音量渐变（淡入 ~1s；暂停曲目时静音）
            if (bgm != null)
            {
                float target = string.IsNullOrEmpty(bgmName) ? 0f : BgmVol;
                bgm.volume = Mathf.MoveTowards(bgm.volume, target, Time.deltaTime * 0.4f);
            }
        }

        /// <summary>播放一次性音效（按素材文件名）。</summary>
        public void Play(string name, float volume = 1f)
        {
            if (clips.TryGetValue(name, out AudioClip c))
                sfx.PlayOneShot(c, volume);
        }

        /// <summary>变调播放（脚步/点击变化感；PlayOneShot 用当前 pitch，播完复原）。</summary>
        public void PlayPitched(string name, float volume = 1f, float pitchMin = 0.9f, float pitchMax = 1.1f)
        {
            if (clips.TryGetValue(name, out AudioClip c))
            {
                sfx.pitch = Random.Range(pitchMin, pitchMax);
                sfx.PlayOneShot(c, volume);
                sfx.pitch = 1f;
            }
        }

        /// <summary>UI 点击声（主菜单/结算/设置通用）。</summary>
        public void PlayUiClick() => PlayPitched("click_001", 0.7f, 0.95f, 1.05f);

        /// <summary>脚步声（走/跑两套素材 + pitch 随机：走轻软、跑重快）。
        /// [0.8.9b] 音量 0.5→0.35、pitch 0.9~1.0→1.0~1.1。
        /// [0.8.9d] 走步换 footstep03/08（频谱选材：全包最闷软的两档）、跑步换 06/01（重踏感）。
        /// [0.8.9h] 低通下沉到素材层：footstep03/08 已用 ffmpeg 低通 1.2kHz 重新生成
        /// （高频差 11.1→20.5 / 12.1→21.3，敲击频段削净）。原 [0.8.9e] 播放时 AudioLowPassFilter
        /// 方案废弃：filter 组件 + PlayOneShot 会触发 "Only custom filters can be played" 黄字警告，
        /// 且挂载顺序错误会红字。素材层滤波音色等效、零运行时组件、无警告。</summary>
        public void PlayFootstep(bool running)
        {
            string name = running
                ? (Random.value < 0.5f ? "footstep06" : "footstep01")
                : (Random.value < 0.5f ? "footstep03" : "footstep08");
            PlayPitched(name, running ? 0.85f : 0.35f, running ? 1.0f : 1.05f, running ? 1.15f : 1.15f);
        }

        void OnGUI()
        {
            // [0.9.3+] 设置面板打开时跳过：面板按钮的点击音由 InGameSettings.ReleaseAt 真触发才播，
            // 这里若仍监听 MouseDown，点面板空白也会响（无动作），强化「点了没反应」错觉（用户两次反馈设置点不动）。
            if (Before8AM.UI.InGameSettings.AnyOpen) return;
            // [0.8.9] 全局 UI 点击音：一处覆盖全部 OnGUI 按钮（GUI.Button 在 MouseUp 触发，这里
            // MouseDown 提前响，即便点空也响——可接受，游戏内不存在"点世界"的操作，点击必是 UI）。
            // [0.8.9c] 只置标记不播放：播放移到 Update（见上），避免在 GUI 事件流中调音频 API。
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                uiClickQueued = true;
        }
    }
}
