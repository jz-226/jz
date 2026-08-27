using UnityEngine;

namespace Before8AM.Visual
{
    /// <summary>
    /// 巡夜者视野锥可视化：贴地一张半透明三角形，指向角色前方，长度=视野距离、张角=视野角度。
    /// 让玩家一眼看出每个巡夜者的"技能"——Runner 窄而短（快但盲）、Tracker 宽而长（慢但看得远）、
    /// Scout 居中（用户反馈：巡夜者技能不突出）。
    /// 作为角色子物体自动跟随朝向（NavMeshAgent/追击都会转朝向），无需每帧更新。
    /// </summary>
    public class VisionCone : MonoBehaviour
    {
        public float Range = 12f;
        public float Angle = 70f;
        public Color ConeColor = new Color(0.4f, 0.6f, 1f, 0.18f);

        MeshFilter mf;

        /// <summary>由构建器调用：建三角形网格（本地空间指向 +Z，即角色前方）。</summary>
        public void Init(float range, float angle, Color color)
        {
            Range = range;
            Angle = angle;
            ConeColor = color;

            GameObject meshGo = new GameObject("VisionCone");
            meshGo.layer = 2;   // Ignore Raycast：不参与 NavMesh 烘焙/不影响视线
            meshGo.transform.SetParent(transform, false);
            meshGo.transform.localPosition = new Vector3(0f, 0.06f, 0f);   // 贴地，避免与路面 z-fight

            mf = meshGo.AddComponent<MeshFilter>();
            MeshRenderer mr = meshGo.AddComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            if (mat != null)
            {
                mat.SetColor("_BaseColor", ConeColor);
                mat.SetFloat("_Surface", 1f);   // Transparent
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;         // Transparent
                mr.sharedMaterial = mat;
            }
            Rebuild();
        }

        void Rebuild()
        {
            var mesh = new Mesh();
            Vector3[] verts = new Vector3[3];
            verts[0] = Vector3.zero;
            float half = Angle * 0.5f * Mathf.Deg2Rad;
            verts[1] = new Vector3(Mathf.Sin(-half) * Range, 0f, Mathf.Cos(-half) * Range);
            verts[2] = new Vector3(Mathf.Sin(half) * Range, 0f, Mathf.Cos(half) * Range);
            mesh.vertices = verts;
            mesh.triangles = new int[] { 0, 1, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
        }
    }
}
