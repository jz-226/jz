# 《早八在逃》Unity 核心源码

这是用于作品展示与代码阅读的核心源码包，对应 TapTap 测试版《早八在逃》。完整游戏请前往 [TapTap](https://tap.cn/lRKlLAoXX)，项目录屏可在[作品集](https://jz-226.github.io/jz/)中观看。

## 包含内容

```text
Assets/
├── Scripts/          # 核心游戏、UI、事件、巡逻、交互与编辑器工具
└── Scenes/           # 主菜单、午夜校园、停车场场景
Packages/             # Unity 包与 TapTap SDK 依赖声明
ProjectSettings/      # Unity 项目配置
```

## 核心实现

- `Scripts/Run/`：单局流程、HUD、道具与状态管理
- `Scripts/Patrol/`：巡夜者巡逻状态与增援逻辑
- `Scripts/World/`：交互系统、撤离门与可互动物体
- `Scripts/Events/`：随机事件与事件目录
- `Scripts/Loot/`：时间碎片、宝箱、消耗品与诱饵
- `Scripts/UI/`：主菜单、商城、排名、设置与引导界面
- `Scripts/Editor/`：关卡搭建与构建辅助工具

## 运行说明

项目基于 **Unity 2022.3 LTS**，使用 URP、Input System、AI Navigation 与 TapTap SDK。此目录是为公开展示而整理的源码参考包，未包含大型模型、贴图、音频、构建产物和 Unity `Library/` 缓存，因此不保证可直接构建完整发行版。TapTap Client ID、Client Token 与 Android 签名密码已用占位符替换，需通过本地私有配置提供。

如需查看完整游戏体验，请前往 [TapTap 测试版](https://tap.cn/lRKlLAoXX)。
