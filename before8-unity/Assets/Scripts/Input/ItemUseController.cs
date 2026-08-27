using UnityEngine;
using UnityEngine.InputSystem;
using Before8AM.Run;

namespace Before8AM.Input
{
    /// <summary>
    /// [0.5] PC 道具快捷键：数字键 1-4 使用对应背包道具（挂 RunManager 同物体）。
    /// [0.8.1] 回退：4→10→4，数字键 1-4 对应道具 1-4（与 RunHUD 键位提示一致）。
    /// 仅 Running 态生效；手游用 MobileControls 的按钮，走同一 RunManager.TryUseItem。
    /// </summary>
    public class ItemUseController : MonoBehaviour
    {
        // [0.8.0] 主键盘 + 小键盘双支持；顺序 = RunItem 索引（0-3 → 数字1-4）
        static readonly Key[] PcKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0,
        };
        static readonly Key[] NumKeys =
        {
            Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5,
            Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9, Key.Numpad0,
        };

        RunManager run;

        void Start()
        {
            run = RunManager.Instance;
        }

        void Update()
        {
            if (run == null || run.State != RunState.Running) return;
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            for (int i = 0; i < ItemCatalog.Count && i < PcKeys.Length; i++)
            {
                if (kb[PcKeys[i]].wasPressedThisFrame || kb[NumKeys[i]].wasPressedThisFrame)
                {
                    run.TryUseItem((RunItem)i);
                    return;   // 一帧只用一个（原 else-if 语义）
                }
            }
        }
    }
}
