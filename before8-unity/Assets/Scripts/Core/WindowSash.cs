using System.Collections;
using UnityEngine;

namespace Before8AM.Core
{
    /// <summary>
    /// 南墙翻窗的"窗扇"：默认关着（盖住翻窗缺口），翻窗开场时绕**上缘铰链**向墙外掀上去
    /// （上悬窗式：从里往外推，下缘向外挑高像雨棚），玩家从缺口穿过去——
    /// 比一堵敞开的洞口更有翻窗的真实感（用户反馈：窗户不像窗户，要有打开的动作）。
    /// 挂在铰链 pivot 上，子物体 Sash 向下伸展覆盖缺口；只负责旋转动画，碰撞已移除。
    /// </summary>
    public class WindowSash : MonoBehaviour
    {
        [Tooltip("掀开角度：绕上缘 X 轴往墙外（南）掀上去（下缘向外挑高）")]
        public float OpenAngle = 95f;
        public float OpenDuration = 1.2f;

        bool opened;
        public bool IsOpen => opened;

        /// <summary>由 WindowIntro 在爬窗前调用：从里往外推、掀开窗扇。</summary>
        public void OpenNow()
        {
            if (opened) return;
            opened = true;
            StartCoroutine(OpenAnim());
        }

        IEnumerator OpenAnim()
        {
            Quaternion start = transform.rotation;
            Quaternion end = transform.rotation * Quaternion.Euler(OpenAngle, 0f, 0f);
            float t = 0f;
            while (t < OpenDuration)
            {
                t += Time.deltaTime;
                transform.rotation = Quaternion.Slerp(start, end, Mathf.SmoothStep(0f, 1f, t / OpenDuration));
                yield return null;
            }
            transform.rotation = end;
        }
    }
}
