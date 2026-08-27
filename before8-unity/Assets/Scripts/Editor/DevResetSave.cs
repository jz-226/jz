using UnityEditor;
using UnityEngine;

namespace Before8AM.EditorTools
{
    /// <summary>
    /// [0.9.2] 开发工具：一键清除 PlayerPrefs 存档 = 回到新玩家状态
    /// （首次启动过场漫画重新播放、金币/等级/段位分/图鉴/皮肤/地图解锁全部归零）。
    /// Editor-only 菜单，不进打包产物；上架验收前刷号、回归测试用。
    /// </summary>
    public static class DevResetSave
    {
        [MenuItem("Tools/早八在逃/重置存档（新玩家模式）")]
        static void ResetAll()
        {
            if (!EditorUtility.DisplayDialog("重置存档",
                "将清除全部本地存档：\n" +
                "· 金币 / 等级 / 段位分 归零\n" +
                "· 已解锁地图 / 皮肤 / 图鉴 清空\n" +
                "· 开场漫画标记清除（下次进入重新播放）\n" +
                "· 设置类（BGM/音效/提示）恢复默认\n\n" +
                "确定要重置为全新玩家吗？",
                "重置", "取消"))
                return;

            PlayerPrefs.DeleteAll();
            Debug.Log("[DevResetSave] 存档已全部清除 —— 现在是新玩家状态，Play 即可从头体验");
        }
    }
}
