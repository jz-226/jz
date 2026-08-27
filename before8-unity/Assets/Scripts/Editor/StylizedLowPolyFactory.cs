using System.Collections.Generic;
using UnityEngine;

namespace Before8AM.EditorTools
{
    /// <summary>
    /// TUNIC 方向的程序化低模构件：统一使用收腰斜面、六边形截面和清晰的大块轮廓。
    /// 仅由编辑器场景生成器调用；所有可见构件默认无 Collider、layer 2。
    /// </summary>
    public static class StylizedLowPolyFactory
    {
        public static GameObject CreateTaperedPrism(string name, Vector3 position, Vector2 bottomSize,
            Vector2 topSize, float height, int sides, Material material, Transform parent = null)
        {
            sides = Mathf.Max(3, sides);
            var vertices = new List<Vector3>(sides * 2 + 2);
            var triangles = new List<int>(sides * 12);
            float halfHeight = height * 0.5f;
            for (int i = 0; i < sides; i++)
            {
                float angle = (Mathf.PI * 2f * i / sides) + Mathf.PI * 0.25f;
                float c = Mathf.Cos(angle);
                float s = Mathf.Sin(angle);
                vertices.Add(new Vector3(c * bottomSize.x * 0.5f, -halfHeight, s * bottomSize.y * 0.5f));
                vertices.Add(new Vector3(c * topSize.x * 0.5f, halfHeight, s * topSize.y * 0.5f));
            }

            int bottomCenter = vertices.Count;
            vertices.Add(Vector3.down * halfHeight);
            int topCenter = vertices.Count;
            vertices.Add(Vector3.up * halfHeight);
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                int b0 = i * 2;
                int t0 = b0 + 1;
                int b1 = next * 2;
                int t1 = b1 + 1;
                triangles.Add(b0); triangles.Add(t0); triangles.Add(b1);
                triangles.Add(b1); triangles.Add(t0); triangles.Add(t1);
                triangles.Add(bottomCenter); triangles.Add(b1); triangles.Add(b0);
                triangles.Add(topCenter); triangles.Add(t0); triangles.Add(t1);
            }
            return CreateMeshObject(name, position, BuildMesh(name, vertices, triangles), material, parent);
        }

        public static GameObject CreateLocalTaperedPrism(Transform parent, string name, Vector3 localPosition,
            Vector2 bottomSize, Vector2 topSize, float height, int sides, Material material)
        {
            GameObject go = CreateTaperedPrism(name, Vector3.zero, bottomSize, topSize, height, sides, material);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go;
        }

        public static GameObject CreateGabledRoof(string name, Vector3 position, float width, float depth,
            float rise, Material material, Transform parent = null)
        {
            float halfW = width * 0.5f;
            float halfD = depth * 0.5f;
            var vertices = new List<Vector3>
            {
                new Vector3(-halfW, 0f, -halfD), new Vector3(halfW, 0f, -halfD),
                new Vector3(-halfW, 0f, halfD), new Vector3(halfW, 0f, halfD),
                new Vector3(-halfW, rise, 0f), new Vector3(halfW, rise, 0f)
            };
            var triangles = new List<int>
            {
                0, 4, 1, 1, 4, 5, 2, 3, 4, 3, 5, 4,
                0, 2, 4, 1, 5, 3, 0, 1, 2, 1, 3, 2
            };
            return CreateMeshObject(name, position, BuildMesh(name, vertices, triangles), material, parent);
        }

        public static GameObject CreateLowPolyTree(string name, Vector3 position, float scale,
            Material trunkMaterial, Material foliageMaterial, Transform parent = null)
        {
            GameObject root = new GameObject(name);
            root.layer = 2;
            root.transform.position = position;
            if (parent != null) root.transform.SetParent(parent, true);

            CreateLocalTaperedPrism(root.transform, "Trunk", new Vector3(0f, scale * 0.52f, 0f),
                new Vector2(scale * 0.26f, scale * 0.26f), new Vector2(scale * 0.16f, scale * 0.16f), scale * 1.04f, 6, trunkMaterial);
            CreateLocalTaperedPrism(root.transform, "Crown_L", new Vector3(-scale * 0.18f, scale * 1.15f, 0f),
                new Vector2(scale * 0.90f, scale * 0.78f), new Vector2(scale * 0.38f, scale * 0.32f), scale * 1.05f, 7, foliageMaterial);
            CreateLocalTaperedPrism(root.transform, "Crown_R", new Vector3(scale * 0.22f, scale * 1.08f, scale * 0.04f),
                new Vector2(scale * 0.82f, scale * 0.72f), new Vector2(scale * 0.32f, scale * 0.28f), scale * 0.92f, 7, foliageMaterial);
            return root;
        }

        public static GameObject CreateLantern(string name, Vector3 position, float height,
            Material poleMaterial, Material glowMaterial, Transform parent = null)
        {
            GameObject root = new GameObject(name);
            root.layer = 2;
            root.transform.position = position;
            if (parent != null) root.transform.SetParent(parent, true);

            CreateLocalTaperedPrism(root.transform, "Base", new Vector3(0f, 0.10f, 0f),
                new Vector2(0.48f, 0.48f), new Vector2(0.30f, 0.30f), 0.20f, 6, poleMaterial);
            CreateLocalTaperedPrism(root.transform, "Pole", new Vector3(0f, height * 0.47f, 0f),
                new Vector2(0.16f, 0.16f), new Vector2(0.10f, 0.10f), height * 0.88f, 6, poleMaterial);
            CreateLocalTaperedPrism(root.transform, "Lantern", new Vector3(0f, height, 0f),
                new Vector2(0.40f, 0.40f), new Vector2(0.28f, 0.28f), 0.38f, 6, glowMaterial);
            CreateLocalTaperedPrism(root.transform, "Cap", new Vector3(0f, height + 0.27f, 0f),
                new Vector2(0.38f, 0.38f), new Vector2(0.16f, 0.16f), 0.18f, 6, poleMaterial);
            return root;
        }

        static GameObject CreateMeshObject(string name, Vector3 position, Mesh mesh, Material material, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.layer = 2;
            go.transform.position = position;
            if (parent != null) go.transform.SetParent(parent, true);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        static Mesh BuildMesh(string name, List<Vector3> vertices, List<int> triangles)
        {
            Mesh mesh = new Mesh { name = name + "_Mesh" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
