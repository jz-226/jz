using UnityEngine;
using Before8AM.Run;
using Before8AM.Collection;
using Before8AM.UI;   // [0.9.3] InGameSettings.AnyOpen

namespace Before8AM.Events
{
    /// <summary>
    /// [0.8.0] 随机事件·午夜商人：发光摊档，碰触弹出面板，花 40 本局金币买随机道具（进背包）。
    /// 数据驱动目录 EventCatalog.MidnightMerchant（World）。多次进出可反复购买，钱不够按钮变灰。
    /// 与商店 ShopController 区别：花的是本局金币（临时），且只能买"随机"道具（商人卖货）。
    /// </summary>
    public class MerchantEvent : MonoBehaviour
    {
        [Tooltip("单次购买价格（本局金币）")]
        public int Price = 40;

        bool panelOpen;
        Transform _t;
        Vector3 basePos;
        bool basePosInit;
        float yaw;   // 慢旋转（摊档不翻滚）

        void Awake() { _t = transform; }

        void Update()
        {
            if (!basePosInit) { basePos = _t.position; basePosInit = true; }
            yaw += 40f * Time.deltaTime;
            _t.rotation = Quaternion.Euler(0f, yaw, 0f);
            _t.position = basePos + Vector3.up * (Mathf.Sin(Time.time * 2.5f) * 0.15f);
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            panelOpen = true;
            Debug.Log("[午夜商人] 「来都来了，买点什么再走？——40 金币一件，童叟无欺。」");
        }

        void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            panelOpen = false;
        }

        void OnGUI()
        {
            if (InGameSettings.AnyOpen) return;   // [0.9.3] 设置面板打开时隐藏商人面板（两面板都居中，商人 GUI.Button 会抢触摸）
            if (!panelOpen) return;
            RunManager run = RunManager.Instance;
            if (run == null || run.State != RunState.Running) return;

            float w = Screen.width, h = Screen.height;
            float pw = Mathf.Min(w * 0.46f, 380f);
            float ph = h * 0.30f;
            float px = (w - pw) * 0.5f, py = (h - ph) * 0.5f;

            GUI.color = new Color(0.10f, 0.09f, 0.06f, 0.95f);
            GUI.DrawTexture(new Rect(px, py, pw, ph), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var title = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(h * 0.028f), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold
            };
            title.normal.textColor = new Color(1f, 0.88f, 0.55f);
            GUI.Label(new Rect(px, py + ph * 0.05f, pw, ph * 0.14f), "午夜商人", title);

            var desc = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(h * 0.02f), alignment = TextAnchor.MiddleCenter
            };
            desc.normal.textColor = new Color(0.85f, 0.85f, 0.8f);
            GUI.Label(new Rect(px + pw * 0.08f, py + ph * 0.22f, pw * 0.84f, ph * 0.3f),
                "「夜里的货，只收你摸到的钱。」\n随机一件道具（进背包）", desc);

            bool canBuy = run.TemporaryCoins >= Price;
            var btn = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(h * 0.022f), alignment = TextAnchor.MiddleCenter
            };
            GUI.enabled = canBuy;
            if (GUI.Button(new Rect(px + pw * 0.1f, py + ph * 0.62f, pw * 0.8f, ph * 0.16f),
                $"购买随机道具  {Price} 金（现有 {run.TemporaryCoins}）", btn))
            {
                if (run.SpendCoins(Price))
                {
                    RunItem item = (RunItem)Random.Range(0, ItemCatalog.Count);
                    run.AddItem(item);
                    CollectionSystem.Unlock(CollectionEntry.MidnightMerchant);
                    Debug.Log($"[午夜商人] 成交：{ItemCatalog.DisplayName(item)} x1（-{Price} 金）");
                }
            }
            GUI.enabled = true;

            if (GUI.Button(new Rect(px + pw * 0.78f, py + ph * 0.02f, pw * 0.2f, ph * 0.12f), "离开",
                new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(h * 0.018f) }))
                panelOpen = false;
        }
    }
}
