# AI 今天吃啥 · Agent 完整搭建文档

> 每一步都写好，照着点就行。不需要写代码。

---

## 第一步：给飞书表格加列（1 分钟）

你刚建的表格默认只有一列。加下面这些列：

| 列名 | 类型 | 说明 |
|------|------|------|
| 日期 | 日期 | 自动填今天 |
| 吃了什么 | 多行文本 | 菜品列表 |
| 主料 | 文本 | 用了什么肉/菜（鸡肉、猪肉、牛肉、鱼虾、豆腐、蔬菜） |
| 花了多少钱 | 数字 | 总花费 |
| 模式 | 单选 | 省钱自己做 / 品质自己做 / 省钱外卖 / 吃食堂 |

加完截图发我。

---

## 第二步：开通飞书 API 权限（3 分钟）

### 2.1 打开飞书开发者后台

浏览器打开：**https://open.feishu.cn/app**

扫码登录 → 点右上角 **"创建企业自建应用"**

### 2.2 填应用信息

| 字段 | 填什么 |
|------|------|
| 应用名称 | 今天吃啥 Agent |
| 应用描述 | 读取和写入吃饭记录表 |
| 头像 | 随便传一张 |

### 2.3 开权限

左侧菜单 → **权限管理** → 搜索 `bitable` → 勾选这两项：

```
☑ bitable:app           （多维表格读写）
☑ bitable:app:readonly  （只读也行，但我们用上面那个）
```

### 2.4 发布应用

右上角 → **"创建应用"** → 然后点 **"版本管理与发布"** → **"创建版本"** → 填 `v1.0` → **"发布"**。

审核通过后（一般秒过），你会看到：

```
App ID:     cli_xxxxxxxxxxxx
App Secret: xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

**把这两个复制下来，发给我。**

---

## 第三步：Dify Workflow 搭建（20 分钟）

### 3.1 新建 Workflow

Dify → **新建应用** → 选 **Workflow**（不是 Chatbot）

名字：**今天吃啥 Agent**

### 3.2 画节点

按顺序拖这 7 个节点：

```
[1. 开始 Start]
    │
    ▼
[2. LLM：意图识别]
    │  Prompt：判断用户是想"推荐新菜"还是"查历史"
    │
    ▼
[3. 条件分支]
    │
    ├─ "推荐新菜" → [4. HTTP：查历史记录]
    │                    │
    │                    ▼
    │               [5. LLM：生成推荐]
    │                    │
    │                    ▼
    │               [6. HTTP：写入记录]
    │                    │
    │                    ▼
    │               [7. 拼接输出]
    │
    └─ "查历史" → [HTTP：查历史记录] → 输出
```

### 3.3 节点详细配置

#### 节点 2：LLM 意图识别

```
System Prompt：
判断用户意图。如果用户说"中午吃啥/推荐/吃啥/今天吃"→ 意图=recommend。
如果用户说"这周吃了什么/历史/记录/趋势"→ 意图=history。
只输出一个词：recommend 或 history。
```

#### 节点 4：HTTP 查历史记录（推荐前先看最近吃了啥）

```
方法：GET
URL：https://open.feishu.cn/open-apis/bitable/v1/apps/O2CvwV7H6i6f0Nkj7Dhc6rsGncZ/tables/tbl4fTtUZZGMCnXb/records?page_size=20
请求头：
  Authorization: Bearer {{飞书token变量}}
  Content-Type: application/json
```

#### 节点 5：LLM 生成推荐（核心）

```
System Prompt：
粘贴 今天吃啥_Agent_完整版.md 里的 Prompt
（角色 + 模式系统 + 追问规则 + 记忆系统 + 推荐规则 + 输出格式）
```

注意：把 HTTP 节点 4 返回的历史数据拼进 Prompt 里——替换"记忆系统"那段的说明，改成：

```
以下是用户最近 3 天的真实菜单：
{{节点4返回的JSON数据}}

基于以上真实数据，自动避开最近出现过的主料。
```

#### 节点 6：HTTP 写入记录（推荐完存档）

```
方法：POST
URL：https://open.feishu.cn/open-apis/bitable/v1/apps/O2CvwV7H6i6f0Nkj7Dhc6rsGncZ/tables/tbl4fTtUZZGMCnXb/records
请求头：
  Authorization: Bearer {{飞书token变量}}
  Content-Type: application/json
Body：
{
  "fields": {
    "日期": {{当天日期}},
    "吃了什么": {{推荐的菜品文本}},
    "主料": {{提取的主料}},
    "花了多少钱": {{总价数字}},
    "模式": {{用户选的模式}}
  }
}
```

---

## 第四步：飞书 API Token 自动刷新

飞书 Token 2 小时过期。在 Dify 里加一个定时刷新机制：

### 4.1 添加变量

Dify 左侧 → **变量** → 添加：

| 变量名 | 值 |
|------|------|
| `feishu_token` | `placeholder`（运行时会自动刷新） |

### 4.2 在 Workflow 最前面加一个 HTTP 节点

```
方法：POST
URL：https://open.feishu.cn/open-apis/auth/v3/tenant_access_token/internal
Body：
{
  "app_id": "你的App ID",
  "app_secret": "你的App Secret"
}
输出：取返回的 tenant_access_token → 赋给变量 feishu_token
```

---

## 第五步：测试

### 测 1：新用户推荐

输入："中午吃啥，自己做，20块"

期望：
- 出推荐（1荤1素，总价≤20）
- HTTP 写入成功（去飞书表格刷新，能看到新增一行）
- 末尾标注 💾已存档

### 测 2：去重

输入："明天继续，自己做，20块"

期望：
- 主料跟上次不重复
- 飞书表格有第二行记录

### 测 3：查历史

输入："这周吃了什么"

期望：
- 翻飞书表格最近7天记录
- 按日期列出

---

## 第六步：跟 Chatbot 版对照（面试用）

| | Chatbot 版 | Agent 版（现在这个） |
|------|:--:|:--:|
| 记忆 | 聊天记录，清了就忘 | 飞书表格，永远在 |
| 去重 | 翻聊天记录找上次推荐 | HTTP 查飞书表，1 秒精确找到 |
| 查历史 | 翻不过来 | HTTP 查表，"最近7天吃了什么"一键出 |
| 跨设备 | 换手机记忆全丢 | 飞书在云端，所有设备共享 |
| 是 Agent 吗 | ❌ | ✅ 有工具（飞书API）+ 有持久记忆 + 有状态 |

---

## 📎 文件索引

| 文件 | 内容 |
|------|------|
| `今天吃啥_Agent_完整版.md` | 核心 Prompt |
| `今天吃啥_Agent搭建_完整文档.md` | 本篇——完整搭建指南 |

---

> 下一步：你发飞书 App ID 和 App Secret 给我 → 我帮你把 HTTP 节点配好 → 你导入 Dify 就能跑。
