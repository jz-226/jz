/* store.js — localStorage 存档层(单机单档)+ 静态装备数据。
   把 Flask 版的 7 张表折叠成一份 JSON 存档:user / userWords / inventory /
   regionProgress / daily / battles(答题流水只做聚合计数,防 localStorage 容量爆)。

   注意:本文件由 build_web.py 从 tools/web_js/ 复制进 web/js/,手改只在 tools/web_js/。 */

const SAVE_KEY = "wordquest_save_v1";

// ── 日期工具(本地时区,与 SQLite date('now','localtime') 语义一致) ──
function _p2(n) { return String(n).padStart(2, "0"); }
function todayStr() {
  const d = new Date();
  return `${d.getFullYear()}-${_p2(d.getMonth() + 1)}-${_p2(d.getDate())}`;
}
function nowStr() {
  const d = new Date();
  return `${todayStr()} ${_p2(d.getHours())}:${_p2(d.getMinutes())}:${_p2(d.getSeconds())}`;
}
function addDaysStr(dateStr, days) {
  const [y, m, d] = dateStr.split("-").map(Number);
  const dt = new Date(y, m - 1, d + days);
  return `${dt.getFullYear()}-${_p2(dt.getMonth() + 1)}-${_p2(dt.getDate())}`;
}

// ── 装备静态数据(与 models/db.py ITEMS_SEED 一致,id 为导入顺序 1-9) ──
const ITEMS = [
  { id: 1,  name: "memory_sword",   name_zh: "记忆之剑",     type: "permanent", effect: "获得 XP +5%",            price: 0,    is_permanent: true,  icon: "icon_sword.svg" },
  { id: 2,  name: "echo_earring",   name_zh: "回响耳环",     type: "permanent", effect: "战斗时可播放一次单词发音", price: 100,  is_permanent: true,  icon: "icon_earring.svg" },
  { id: 3,  name: "insight_eye",    name_zh: "洞察之眼",     type: "consumable", effect: "答题时排除一个错误答案",  price: 150,  is_permanent: false, icon: "icon_eye.svg" },
  { id: 4,  name: "etymology_book", name_zh: "词源之书",     type: "permanent", effect: "显示词根提示",           price: 200,  is_permanent: true,  icon: "icon_book.svg" },
  { id: 5,  name: "guard_shield",   name_zh: "守护之盾",     type: "consumable", effect: "免疫一次答错伤害",       price: 120,  is_permanent: false, icon: "icon_shield.svg" },
  { id: 6,  name: "berserker_blade", name_zh: "狂战士之刃",  type: "permanent", effect: "Combo≥5 时伤害+50%",     price: 300,  is_permanent: true,  icon: "icon_sword.svg" },
  { id: 7,  name: "knight_skin",    name_zh: "铁甲骑士·皮肤", type: "skin",     effect: "外观:西幻·银甲骑士 / 东方·青衫侠客", price: 500,  is_permanent: true,  icon: "icon_sword.svg" },
  { id: 8,  name: "warrior_skin",   name_zh: "辉金圣武士·皮肤", type: "skin",   effect: "外观:西幻·辉金圣武士 / 东方·玄袍道长", price: 1200, is_permanent: true,  icon: "icon_sword.svg" },
  { id: 9,  name: "mage_skin",      name_zh: "秘法法师·皮肤", type: "skin",     effect: "外观:西幻·秘法法师 / 东方·御剑书生", price: 2500, is_permanent: true,  icon: "icon_sword.svg" },
];
const itemById = (id) => ITEMS.find((i) => i.id === id) || null;
// 皮肤商品名 → avatar 存的皮肤 key(与 game/shop.py SKIN_KEYS 一致)
const SKIN_KEYS = { knight_skin: "knight", warrior_skin: "warrior", mage_skin: "mage" };

const Store = {
  _s: null,

  load() {
    if (this._s) return this._s;
    let raw = null;
    try { raw = localStorage.getItem(SAVE_KEY); } catch (e) { /* 隐私模式等 */ }
    if (raw) {
      try { this._s = JSON.parse(raw); } catch (e) { this._s = null; }
    }
    if (!this._s) this._s = this._default();
    return this._s;
  },

  persist() {
    try { localStorage.setItem(SAVE_KEY, JSON.stringify(this._s)); } catch (e) { /* 内存兜底 */ }
  },

  _default() {
    return {
      v: 1,
      user: {
        id: 1, username: "", nickname: "冒险者", avatar: "adventurer",
        target_score: 425, daily_minutes: 20, theme: "western",
        onboarded: 0, level: 1, xp: 0, gold: 0,
        hp: 100, max_hp: 100, attack: 10, unlocked_region: 0,
      },
      userWords: {},      // word_id → {status, mastery, correct_count, ...}
      inventory: {},      // item_id → {equipped, quantity}
      regionProgress: {}, // region_idx → kills
      daily: this._freshDaily(),
      battles: { total: 0, correct: 0, by_diff: { 1: { t: 0, r: 0 }, 2: { t: 0, r: 0 }, 3: { t: 0, r: 0 }, 4: { t: 0, r: 0 } } },
    };
  },

  _freshDaily() {
    return { task_date: todayStr(), monsters_killed: 0, words_learned: 0, words_reviewed: 0, max_combo: 0, completed: 0 };
  },

  // 每日任务跨天自动重置
  _rollDaily(s) {
    if (!s.daily || s.daily.task_date !== todayStr()) {
      s.daily = this._freshDaily();
      this.persist();
    }
  },

  // ── 用户 ──
  getUser() { const s = this.load(); this._rollDaily(s); return s.user; },
  patchUser(updates) { Object.assign(this.getUser(), updates); this.persist(); },

  // ── 词档案(user_words) ──
  getUW(wid) { return this.load().userWords[wid] || null; },
  putUW(wid, rec) { this.load().userWords[wid] = rec; this.persist(); },
  // 返回带 word_id 的行(对应 Flask user_words 表的 word_id 列)。
  // 坑:存档里 word_id 是对象的 key,记录本体没有该字段 → 词藏筛选/复习队列全靠它,
  // 之前漏了导致 unseen 全显示、weak/learning/mastered 全空(词藏单词"消失")。
  allUserWords() {
    return Object.entries(this.load().userWords).map(([id, rec]) => ({ ...rec, word_id: Number(id) }));
  },

  // ── 背包(inventory) ──
  getInvItem(iid) { return this.load().inventory[iid] || null; },
  setInvItem(iid, rec) { this.load().inventory[iid] = rec; this.persist(); },
  removeInvItem(iid) { delete this.load().inventory[iid]; this.persist(); },
  allInventory() { return this.load().inventory; },

  // ── 区域击杀进度 ──
  getRegionKills(idx) { return this.load().regionProgress[idx] || 0; },
  addRegionKill(idx) {
    const s = this.load();
    s.regionProgress[idx] = (s.regionProgress[idx] || 0) + 1;
    this.persist();
  },

  // ── 每日任务 ──
  getDaily() { const s = this.load(); this._rollDaily(s); return s.daily; },
  bumpDaily({ monsters_killed = 0, words_learned = 0, words_reviewed = 0 } = {}) {
    const d = this.getDaily();
    d.monsters_killed += monsters_killed;
    d.words_learned += words_learned;
    d.words_reviewed += words_reviewed;
    this.persist();
    return d;
  },
  setDailyCombo(combo) {
    const d = this.getDaily();
    if (combo > d.max_combo) { d.max_combo = combo; this.persist(); }
  },
  setDailyCompleted() {
    const d = this.getDaily();
    if (!d.completed) { d.completed = 1; this.persist(); }
  },

  // ── 答题流水聚合(battles 只计数,不存明细,防容量爆) ──
  getBattles() { return this.load().battles; },
  recordBattle(wid, correct, difficulty) {
    const b = this.load().battles;
    b.total += 1;
    if (correct) b.correct += 1;
    const dd = b.by_diff[difficulty] || (b.by_diff[difficulty] = { t: 0, r: 0 });
    dd.t += 1;
    if (correct) dd.r += 1;
    this.persist();
  },

  reset() { this._s = this._default(); this.persist(); },
};
