using UnityEngine;

namespace Before8AM.World
{
    /// <summary>
    /// 可交互物体基类。InteractionSystem 负责检测与触发。
    /// 支持瞬时交互（Interact）与持续交互（Progress 0~1，如搜索宝箱/开启晨门）。
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        [Tooltip("玩家中心到交互物的距离小于该值才可交互")]
        public float InteractionRange = 3f;

        /// <summary>HUD 提示文字。</summary>
        public abstract string PromptText { get; }

        /// <summary>是否需要按住交互键持续一段时间。</summary>
        public virtual bool RequiresHold => false;

        /// <summary>持续交互总时长（秒）。</summary>
        public virtual float HoldDuration => 1f;

        /// <summary>玩家按住交互键时的持续进度回调（0~1）。</summary>
        public virtual void OnHoldProgress(float progress01) { }

        /// <summary>瞬时交互，或持续交互完成时调用。</summary>
        public abstract void Interact();

        public virtual bool CanInteract => true;
    }
}
