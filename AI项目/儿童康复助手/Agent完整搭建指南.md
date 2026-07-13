# AI 儿童康复家庭训练助手 · Agent 完整搭建

> 姜梓 · 2026.07.08  
> 从 Bot 升级到 Agent：加记忆、加工具、加状态追踪

---

## 一、Agent 架构图

```
康复师端（Dify Workflow）
    │
    │ "小明4岁，唇力训练，吹纸片每天2次10下"
    │
    ▼
┌─────────────────────────────────────┐
│  Dify Workflow（Agent 大脑）          │
│                                      │
│  1. LLM：生成家长版方案                 │
│  2. HTTP：写入 training_plans 表        │
│  3. HTTP：检查今日是否已有方案            │
│  4. 拼接：方案 + 小程序链接               │
│  5. 输出：完整的家长版方案                │
└──────────┬──────────────────────────┘
           │
    康复师转发到家长微信
           │
           ▼
┌─────────────────────────────────────┐
│  家长端（微信小程序）                   │
│                                      │
│  页面1：今日训练 card                  │
│  · 调用云开发 API 读 training_plans    │
│  · 显示训练步骤 + 打卡按钮              │
│  · 点打卡 → 调用 API 写 checkins 表     │
│                                      │
│  页面2：进度看板                        │
│  · 本周完成率 进度条                    │
│  · 连续打卡天数 火焰                    │
└──────────┬──────────────────────────┘
           │
           ▼
┌─────────────────────────────────────┐
│  康复师端（查看反馈）                   │
│                                      │
│  康复师输入："小明这周怎么样"            │
│  → Agent 读 checkins 表                │
│  → 计算完成率 + 趋势                    │
│  → 输出："完成率71%，吹气从5cm→12cm，   │
│     夹压舌板配合度低，建议下周调整"       │
└─────────────────────────────────────┘
```

---

## 二、数据库设计（微信云开发）

打开微信开发者工具 → 云开发控制台 → 数据库 → 新建三个集合：

### 集合 1：children

```json
{
  "_id": "自动生成",
  "name": "小明",
  "age": 4,
  "condition": "唇力不足",
  "therapist_openid": "康复师的微信openid",
  "parent_openid": "家长的微信openid",
  "created_at": "2026-07-08"
}
```

### 集合 2：training_plans

```json
{
  "_id": "自动生成",
  "child_id": "children表里的_id",
  "child_name": "小明",
  "start_date": "2026-07-08",
  "end_date": "2026-07-14",
  "exercises": [
    {
      "name": "吹纸片比赛",
      "icon": "red_circle",
      "frequency": "每天2次，每次10下",
      "steps": ["准备吸管和纸球", "坐直吹球"],
      "game": "比赛看谁吹得远",
      "warning": "吹到脸红就停"
    },
    {
      "name": "魔法棒挑战",
      "icon": "yellow_circle",
      "frequency": "每天3组，每组3次",
      "steps": ["嘴唇夹压舌板", "大人轻拽"],
      "game": "夹住变身超人",
      "warning": "嘴唇发紫就停"
    }
  ],
  "created_at": "2026-07-08"
}
```

### 集合 3：checkins

```json
{
  "_id": "自动生成",
  "plan_id": "training_plans的_id",
  "child_id": "孩子的_id",
  "exercise_index": 0,
  "session": 1,
  "date": "2026-07-08",
  "completed_at": "2026-07-08T09:15:00Z"
}
```

### 数据库权限设置（MVP）

每个集合 → 权限设置：
- 读：所有用户可读
- 写：所有用户可写

---

## 三、Dify Workflow 搭建（康复师端）

### 节点链

```
[开始]
  │
  ▼
[LLM：生成方案]  ← 你那套 v2 Prompt
  │
  ▼
[代码节点：提取数据]  ← 从LLM输出提取 child_name, exercises
  │
  ▼
[HTTP：写 training_plans]  ← POST 到云开发API
  │
  ▼
[拼接输出]  ← 方案文字 + 小程序链接
  │
  ▼
[结束]
```

### HTTP 节点配置

**写入方案**：
```
方法：POST
URL：https://api.weixin.qq.com/tcb/databaseadd?access_token=你的token
Body：
{
  "env": "你的云环境ID",
  "query": "db.collection('training_plans').add({...})"
}
```

**获取 access_token**（每 2 小时刷一次）：
```
GET https://api.weixin.qq.com/cgi-bin/token?grant_type=client_credential&appid=你的AppID&secret=你的AppSecret
```

> AppID 和 AppSecret 去微信公众平台 → 开发管理 → 开发设置 里找。

---

## 四、家长端小程序（2 个页面）

用学情本小程序骨架，加两个新页面。

### 页面 1：pages/training/training

打开小程序 → 显示今日训练 → 点打卡。

**training.js 核心逻辑**：
```javascript
Page({
  data: { childName: '', exercises: [], streakDays: 0, planId: '' },

  onLoad(options) {
    this.setData({ planId: options.planId });
    this.loadPlan(options.planId);
  },

  async loadPlan(planId) {
    const db = wx.cloud.database();
    const plan = await db.collection('training_plans').doc(planId).get();
    const today = new Date().toISOString().slice(0,10);
    const checks = await db.collection('checkins')
      .where({ plan_id: planId, date: today }).get();

    const exercises = plan.data.exercises.map((ex, i) => ({
      ...ex,
      sessions: [1,2,3].slice(0, 2).map(n => ({
        num: n,
        done: checks.data.some(c => c.exercise_index === i && c.session === n)
      }))
    }));

    this.setData({ childName: plan.data.child_name, exercises });
    this.calcStreak(plan.data.child_id);
  },

  async doCheckin(e) {
    const { ex, session } = e.currentTarget.dataset;
    const db = wx.cloud.database();
    await db.collection('checkins').add({
      data: {
        plan_id: this.data.planId,
        exercise_index: ex,
        session: session,
        date: new Date().toISOString().slice(0,10),
        completed_at: new Date().toISOString()
      }
    });
    this.loadPlan(this.data.planId);
    wx.showToast({ title: '打卡成功', icon: 'success' });
  },

  async calcStreak(childId) {
    const db = wx.cloud.database();
    let streak = 0;
    for (let i = 0; i < 30; i++) {
      const d = new Date(); d.setDate(d.getDate() - i);
      const ds = d.toISOString().slice(0,10);
      const res = await db.collection('checkins')
        .where({ child_id: childId, date: ds }).count();
      if (res.total > 0) streak++; else break;
    }
    this.setData({ streakDays: streak });
  }
});
```

### 页面 2：pages/progress/progress

本周完成率 + 每项训练进度条。

---

## 五、康复师端反馈查询

Workflow 加一个分支：当 query 含"打卡/这周/怎么样"时触发。

```
[条件分支]
  │
  ▼
[HTTP：查 checkins 表]
  │
  ▼
[LLM：生成反馈摘要]
  │  输入：打卡记录
  │  输出：完成率+趋势+建议
  │
  ▼
[结束]
```

---

## 六、实施步骤

| 步骤 | 做什么 | 时间 |
|:--:|------|:--:|
| 1 | 微信开发者工具 → 云开发 → 建 3 个集合 | 10 分钟 |
| 2 | Dify Chatflow 升级为 Workflow → 加 HTTP 节点 | 1 小时 |
| 3 | 学情本骨架 → 新建 2 个页面 | 1 小时 |
| 4 | 联调：出方案→写库→小程序读取→打卡→回写 | 1 小时 |
| 5 | 加反馈查询分支 | 30 分钟 |

---

## 七、面试对照表

| Agent 特征 | 怎么实现的 |
|------|------|
| **察** Observe | HTTP 节点读数据库 |
| **想** Think | LLM 节点推理 |
| **动** Act | HTTP 写库 + 小程序打卡 |
| **记忆** Memory | 云数据库持久化 |
| **工具** Tools | 云开发 HTTP API |
| **评测** Eval | 打卡率数据量化 |
