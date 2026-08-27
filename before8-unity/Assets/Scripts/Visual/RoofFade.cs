using UnityEngine;

namespace Before8AM.Visual
{
    /// <summary>
    /// 屋顶淡出：玩家进入本建筑内部时屋顶透明度降到最低（不再遮挡俯视的"我"），
    /// 离开后平滑恢复。用 建筑中心+尺寸 做位置判断（不依赖 tag，精确到每栋建筑，互不干扰）。
    /// 挂在每栋建筑的屋顶容器上，控制其下所有屋顶片（两片斜板 + 顶脊）的透明度。
    /// </summary>
    public class RoofFade : MonoBehaviour
    {
        public Vector3 BuildingCenter;
        public Vector3 BuildingSize = new Vector3(10f, 4f, 6f);
        [Tooltip("淡出/恢复速度（越大越快）")] public float FadeSpeed = 4f;
        [Tooltip("玩家在建筑内时屋顶透明度（近乎消失，但仍留一丝轮廓提示有屋顶）")] public float HiddenAlpha = 0.05f;
        [Tooltip("正常屋顶透明度（半透明，能看进内部）")] public float VisibleAlpha = 0.45f;

        Material[] roofMats;   // 每栋建筑实例化自己的材质（独立控制，互不影响）
        float current;
        Transform cachedPlayer;

        public void Setup(Vector3 center, Vector2 size)
        {
            BuildingCenter = center;
            BuildingSize = new Vector3(size.x, 4f, size.y);
        }

        void Awake()
        {
            var rs = GetComponentsInChildren<Renderer>(true);
            roofMats = new Material[rs.Length];
            for (int i = 0; i < rs.Length; i++) roofMats[i] = rs[i].material;
            current = VisibleAlpha;
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) cachedPlayer = p.transform;
        }

        void Update()
        {
            if (cachedPlayer == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p == null) return;
                cachedPlayer = p.transform;
            }

            Vector3 half = BuildingSize * 0.5f;
            Vector3 d = cachedPlayer.position - BuildingCenter;
            bool inside = Mathf.Abs(d.x) <= half.x && Mathf.Abs(d.z) <= half.z;

            current = Mathf.Lerp(current, inside ? HiddenAlpha : VisibleAlpha, FadeSpeed * Time.deltaTime);
            for (int i = 0; i < roofMats.Length; i++)
            {
                Color c = roofMats[i].color;
                c.a = current;
                roofMats[i].color = c;
            }
        }
    }
}
