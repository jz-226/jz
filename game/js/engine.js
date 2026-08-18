/* engine.js — 本地规则引擎:把 Flask 后端的 25 个 API 端点复现在浏览器里。
   app.js 的 api() 只调 localApi(path, opts),前端其余 1900 行零改动。
   所有规则逐行对照 app.py + game/*.py 翻译,返回 JSON 结构与 Flask 完全一致。

   注意:本文件由 build_web.py 从 tools/web_js/ 复制进 web/js/,手改只在 tools/web_js/。 */

// ═══════════ 词库索引 ═══════════
const WORDS = SEED_WORDS;                       // js/words.js,4539 词
const wordsById = {};
const wordsByDiff = {};
for (const w of WORDS) {
  wordsById[w.id] = w;
  (wordsByDiff[w.difficulty] = wordsByDiff[w.difficulty] || []).push(w);
}

function shuffle(arr) {
  for (let i = arr.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [arr[i], arr[j]] = [arr[j], arr[i]];
  }
  return arr;
}
function randomWord(diff) {
  const pool = diff != null ? (wordsByDiff[diff] || []) : WORDS;
  if (!pool.length) return null;
  return pool[Math.floor(Math.random() * pool.length)];
}

// ═══════════ 进度系统(game/progression.py) ═══════════
function xpToNext(level) { return Math.floor(100 * Math.pow(level, 1.4)); }
function xpForLevel(level) {
  let total = 0;
  for (let lv = 1; lv < level; lv++) total += xpToNext(lv);
  return total;
}
function levelFromXp(xp) {
  let level = 1, remaining = xp;
  while (remaining >= xpToNext(level)) { remaining -= xpToNext(level); level += 1; }
  return { level, remaining };
}
// 加 XP + 处理升级(升一级 +10 最大HP +1 攻击,HP 回满)
function addXp(amount) {
  const u = Store.getUser();
  const newXp = u.xp + amount;
  const oldLevel = u.level;
  const nl = levelFromXp(newXp);
  if (nl.level > oldLevel) {
    const d = nl.level - oldLevel;
    const maxHp = u.max_hp + d * 10;
    Store.patchUser({ xp: newXp, level: nl.level, max_hp: maxHp, hp: maxHp, attack: u.attack + d });
    return { leveled_up: true, new_level: nl.level, xp: newXp, bonus_hp: d * 10, bonus_atk: d };
  }
  Store.patchUser({ xp: newXp });
  return { leveled_up: false, new_level: nl.level, xp: newXp, bonus_hp: 0, bonus_atk: 0 };
}
function addGold(amount) {
  const u = Store.getUser();
  Store.patchUser({ gold: u.gold + amount });
}

// ═══════════ 测评规则(game/assessment.py) ═══════════
const ASSESSMENT_COUNT = 16;
const DIFFICULTY_POOL = [1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4];
const SCORE_TIERS = [
  [0, 1, 0], [6, 2, 1], [8, 3, 2], [10, 4, 2],
  [12, 5, 3], [14, 6, 3], [15, 7, 4], [16, 9, 5],
];

function buildOptions(word, count = 4) {
  const correct = word.meaning;
  const pool = wordsByDiff[word.difficulty] || [];
  const distractors = [];
  const seen = new Set([correct]);
  const candidates = shuffle(pool.filter((w) => w.id !== word.id && w.meaning));
  for (const w of candidates) {
    if (distractors.length >= count - 1) break;
    if (!seen.has(w.meaning)) { distractors.push(w.meaning); seen.add(w.meaning); }
  }
  if (distractors.length < count - 1) {
    for (const w of shuffle(WORDS)) {
      if (distractors.length >= count - 1) break;
      if (w.meaning && !seen.has(w.meaning)) { distractors.push(w.meaning); seen.add(w.meaning); }
    }
  }
  return [shuffle([correct, ...distractors]), correct];
}

function pickAssessmentWords(count = ASSESSMENT_COUNT) {
  const words = [];
  for (const diff of DIFFICULTY_POOL.slice(0, count)) {
    const w = randomWord(diff);
    if (w) words.push(w);
  }
  return words;
}

function evaluateAssessment(results) {
  const correct = results.filter((r) => r.correct).length;
  let level = SCORE_TIERS[0][1], unlockRegion = SCORE_TIERS[0][2];
  for (const [th, lv, reg] of SCORE_TIERS) {
    if (correct >= th) { level = lv; unlockRegion = reg; }
  }
  const byDiff = { 1: 0, 2: 0, 3: 0, 4: 0 };
  for (const r of results) {
    if (r.correct) byDiff[r.difficulty || 2] = (byDiff[r.difficulty || 2] || 0) + 1;
  }
  return {
    correct_count: correct,
    total: results.length,
    initial_level: level,
    unlock_region: unlockRegion,
    easy_correct: byDiff[1],
    medium_correct: byDiff[2],
    hard_correct: byDiff[3] + byDiff[4],
  };
}

// ═══════════ 战斗规则(game/combat.py + monsters.py) ═══════════
const COMBO_DAMAGE = { 1: 10, 2: 15, 3: 20, 4: 30, 5: 40 };
const ATTACK_GROWTH_PER_POINT = 0.02;
const ATTACK_GROWTH_CAP = 2.0;
const MONSTERS = {
  1: { name: "Slime", name_zh: "史莱姆", icon: "", hp: 80, gold: 10 },
  2: { name: "Goblin", name_zh: "哥布林", icon: "", hp: 110, gold: 15 },
  3: { name: "Werewolf", name_zh: "狼人", icon: "", hp: 150, gold: 20 },
  4: { name: "Demon", name_zh: "恶魔", icon: "", hp: 200, gold: 30 },
};
const BOSS_MONSTER = { name: "Dragon", name_zh: "四级龙", icon: "", hp: 350, gold: 100 };
const CHAPTER_BOSSES = [
  { name: "Goblin King", name_zh: "哥布林王", icon: "", hp: 420, gold: 60 },
  { name: "Goblin War Chief", name_zh: "哥布林大酋长", icon: "", hp: 560, gold: 90 },
  { name: "Goblin High King", name_zh: "哥布林国王", icon: "", hp: 700, gold: 120 },
  { name: "Werewolf Alpha", name_zh: "狼王", icon: "", hp: 900, gold: 160 },
  { name: "Demon Lord", name_zh: "深渊魔王", icon: "", hp: 1100, gold: 220 },
  { name: "Dragon Emperor", name_zh: "四级龙王", icon: "", hp: 1400, gold: 320 },
];

function damageForCombo(combo, attack = 10, hasBerserker = false) {
  const base = COMBO_DAMAGE[combo] || 40;
  const growth = Math.min(ATTACK_GROWTH_CAP, 1 + attack * ATTACK_GROWTH_PER_POINT);
  let dmg = Math.floor(base * growth);
  if (hasBerserker && combo >= 5) dmg = Math.floor(dmg * 1.5);
  return dmg;
}
function monsterAttackDamage(playerLevel) { return 15 + playerLevel * 2; }
function evaluateAnswer(correct, combo, playerLevel, attack = 10, hasBerserker = false) {
  if (correct) {
    const newCombo = combo + 1;
    return { correct: true, damage: damageForCombo(newCombo, attack, hasBerserker), combo: newCombo, player_hurt: 0 };
  }
  return { correct: false, damage: 0, combo: 0, player_hurt: monsterAttackDamage(playerLevel) };
}

// ═══════════ 主题映射(app.py) ═══════════
const MAP_REGIONS = [
  { id: 1, name: "新手村", icon: "scene_grassland.svg", monster: "史莱姆", boss: true },
  { id: 2, name: "哥布林森林", icon: "scene_forest.svg", monster: "哥布林", boss: true },
  { id: 3, name: "哥布林城", icon: "scene_fort.svg", monster: "哥布林", boss: true },
  { id: 4, name: "狼人荒原", icon: "scene_wasteland.svg", monster: "狼人", boss: true },
  { id: 5, name: "恶魔峡谷", icon: "scene_volcano.svg", monster: "恶魔", boss: true },
  { id: 6, name: "四级龙巢", icon: "scene_dragonnest.svg", monster: "四级龙", boss: true },
];
const REGION_KILL_TARGETS = [500, 800, 1200, 1600, 2000];
const THEME_MONSTER_MAP = {
  western: {},
  east: {
    "史莱姆": "草木小妖", "哥布林": "青竹狐妖", "狼人": "苍狼妖", "恶魔": "赤魇", "四级龙": "深渊蛟龙",
    "哥布林王": "草木妖王", "哥布林大酋长": "青竹狐王", "哥布林国王": "狐妖大君",
    "狼王": "苍狼王", "深渊魔王": "炎魔大君", "四级龙王": "深渊龙皇",
  },
};
const THEME_REGION_NAMES = {
  western: ["新手村", "哥布林森林", "哥布林城", "狼人荒原", "恶魔峡谷", "四级龙巢"],
  east: ["青山村", "青竹密林", "狐妖岭", "苍狼荒原", "赤焰山", "深渊龙潭"],
};
function themedMonsterName(theme, westernName) {
  return (THEME_MONSTER_MAP[theme] || THEME_MONSTER_MAP.western)[westernName] || westernName;
}
function combatMonster(difficulty, theme = "western", isBoss = false, regionIdx = null) {
  let m;
  if (isBoss) m = CHAPTER_BOSSES[regionIdx != null ? regionIdx : difficulty - 1] || BOSS_MONSTER;
  else m = difficulty <= 4 ? MONSTERS[difficulty] : BOSS_MONSTER;
  return { name: themedMonsterName(theme, m.name_zh), icon: m.icon, hp: m.hp, max_hp: m.hp, gold: m.gold, difficulty };
}
function nextBattleQuestion(difficulty) {
  const wordDiff = Math.min(difficulty, 4);
  let word = randomWord(wordDiff);
  if (!word) word = randomWord();
  const [options, correct] = buildOptions(word);
  return { word: word.word, word_id: word.id, phonetic: word.phonetic || "", options, correct, difficulty: word.difficulty };
}
function regionUnlocked(idx) {
  if (idx <= 0) return true;
  const u = Store.getUser();
  if (idx <= u.unlocked_region) return true;
  if (idx - 1 < REGION_KILL_TARGETS.length) {
    return Store.getRegionKills(idx - 1) >= REGION_KILL_TARGETS[idx - 1];
  }
  return true;
}

// ═══════════ 奖励规则(game/rewards.py) ═══════════
function xpForCorrect(combo, difficulty, hasMemorySword = false) {
  let xp = (10 + difficulty * 5) + Math.min(20, combo * 2);
  if (hasMemorySword) xp = Math.floor(xp * 1.05);
  return xp;
}
function goldForKill(monsterGold, combo) { return monsterGold + Math.min(15, combo * 2); }
function xpForKill(difficulty, combo) { return 5 + difficulty * 5 + combo; }

// ═══════════ 词藏规则(game/vocab.py) ═══════════
function statusOf(mastery) { return mastery < 50 ? "weak" : (mastery < 80 ? "learning" : "mastered"); }
function reviewIntervalDays(mastery) {
  if (mastery < 50) return 1;
  if (mastery < 80) return 2;
  if (mastery < 90) return 4;
  return 7;
}
function newMastery(old, correct, streak) {
  if (correct) return Math.min(100, old + 6 + Math.min(streak, 4) * 2);
  return Math.max(0, old - 12);
}
function recordAnswer(wordId, correct) {
  const now = nowStr();
  const today = todayStr();
  let rec = Store.getUW(wordId);
  if (!rec) {
    rec = { status: "weak", mastery: 0, correct_count: correct ? 1 : 0, wrong_count: correct ? 0 : 1,
            total_attempts: 0, current_streak: 0, best_streak: 0,
            first_seen: now, last_seen: now, last_correct_at: null, last_wrong_at: null, next_review_at: null };
  } else {
    rec = { ...rec, correct_count: rec.correct_count + (correct ? 1 : 0), wrong_count: rec.wrong_count + (correct ? 0 : 1), last_seen: now };
  }
  const streak = correct ? rec.current_streak + 1 : 0;
  const mastery = newMastery(rec.mastery, correct, streak);
  rec.mastery = mastery;
  rec.total_attempts += 1;
  rec.current_streak = streak;
  rec.best_streak = Math.max(rec.best_streak, streak);
  if (correct) rec.last_correct_at = now; else rec.last_wrong_at = now;
  rec.next_review_at = addDaysStr(today, reviewIntervalDays(mastery));
  rec.status = statusOf(mastery);
  Store.putUW(wordId, rec);
  return rec;
}
function markSeen(wordId) {
  const rec = Store.getUW(wordId);
  if (rec) return rec;
  const now = nowStr();
  const seen = { status: "learning", mastery: 50, correct_count: 0, wrong_count: 0, total_attempts: 0,
                 current_streak: 0, best_streak: 0, first_seen: now, last_seen: now,
                 last_correct_at: null, last_wrong_at: null, next_review_at: addDaysStr(todayStr(), 2) };
  Store.putUW(wordId, seen);
  return seen;
}
function vaultStats() {
  const total = WORDS.length;
  const recs = Store.allUserWords();
  const counts = { weak: 0, learning: 0, mastered: 0 };
  for (const r of recs) if (r.status in counts) counts[r.status] += 1;
  const today = todayStr();
  const due = recs.filter((r) => r.status === "mastered" && r.next_review_at && r.next_review_at <= today).length;
  return { total, unseen: Math.max(0, total - recs.length), weak: counts.weak, learning: counts.learning, mastered: counts.mastered, due_review: due };
}
// 带词信息 + 用户档案的合并行(词藏列表用)
function mergeWordRec(word) {
  const rec = Store.getUW(word.id);
  if (!rec) {
    return { ...word, status: "unseen", mastery: 0, correct_count: 0, wrong_count: 0, total_attempts: 0, current_streak: 0, best_streak: 0, first_seen: null, last_seen: null, next_review_at: null };
  }
  return { ...word, ...rec };
}
function searchWords(status, q, page, size) {
  page = Math.max(1, page | 0);
  size = Math.max(1, Math.min(size | 0, 100));
  let items;
  if (status === "unseen") {
    const seenIds = new Set(Store.allUserWords().map((r) => r.word_id));
    items = WORDS.filter((w) => !seenIds.has(w.id)).map(mergeWordRec);
  } else if (status === "all") {
    items = WORDS.map(mergeWordRec);
  } else {
    items = Store.allUserWords()
      .filter((r) => r.status === status)
      .map((r) => mergeWordRec(wordsById[r.word_id]))
      .filter(Boolean);
  }
  if (q) {
    const needle = q.toLowerCase();
    items = items.filter((w) => w.word.toLowerCase().includes(needle) || (w.meaning || "").includes(q));
  }
  items.sort((a, b) => a.word.localeCompare(b.word, "en", { sensitivity: "base", caseFirst: "lower" }));
  const total = items.length;
  const offset = (page - 1) * size;
  return { total, page, size, items: items.slice(offset, offset + size) };
}
function wordDetail(wordId) {
  const w = wordsById[wordId];
  if (!w) return null;
  const rec = Store.getUW(wordId);
  const vault = rec || { status: "unseen", mastery: 0, correct_count: 0, wrong_count: 0, total_attempts: 0, current_streak: 0, best_streak: 0, first_seen: null, last_seen: null, next_review_at: null };
  return { ...w, vault };
}
function weakWords(limit = 50) {
  return Store.allUserWords()
    .filter((r) => r.status === "weak")
    .sort((a, b) => (a.mastery - b.mastery) || (a.last_seen || "").localeCompare(b.last_seen || ""))
    .slice(0, limit)
    .map((r) => ({ ...mergeWordRec(wordsById[r.word_id]), word_id: r.word_id }))
    .filter((r) => r.word);
}
function dueReview(limit = 50) {
  const today = todayStr();
  return Store.allUserWords()
    .filter((r) => r.status === "mastered" && r.next_review_at && r.next_review_at <= today)
    .sort((a, b) => (a.next_review_at || "").localeCompare(b.next_review_at || ""))
    .slice(0, limit)
    .map((r) => ({ ...mergeWordRec(wordsById[r.word_id]), word_id: r.word_id }))
    .filter((r) => r.word);
}
function unseenWords(limit = 50) {
  const seenIds = new Set(Store.allUserWords().map((r) => r.word_id));
  return shuffle(WORDS.filter((w) => !seenIds.has(w.id))).slice(0, limit);
}

// ═══════════ 商店规则(game/shop.py) ═══════════
function getShop() {
  return { items: ITEMS.map((it) => {
    const inv = Store.getInvItem(it.id);
    return {
      id: it.id, name: it.name, name_zh: it.name_zh, icon: it.icon, type: it.type,
      effect: it.effect, price: it.price, is_permanent: it.is_permanent,
      owned: !!inv && it.is_permanent,
      equipped: !!inv && !!inv.equipped,
    };
  }) };
}
function buyItem(itemId) {
  const it = itemById(itemId);
  if (!it) return { ok: false, message: "装备不存在", gold: null };
  const u = Store.getUser();
  if (u.gold < it.price) return { ok: false, message: "金币不足", gold: u.gold };
  const inv = Store.getInvItem(itemId);
  if (it.is_permanent && inv) return { ok: false, message: "已拥有该装备", gold: u.gold };
  Store.patchUser({ gold: u.gold - it.price });
  if (inv) Store.setInvItem(itemId, { equipped: inv.equipped, quantity: inv.quantity + 1 });
  else Store.setInvItem(itemId, { equipped: 0, quantity: 1 });
  return { ok: true, message: "购买成功:" + it.name_zh, gold: u.gold - it.price };
}
function getInventory() {
  return { items: Object.entries(Store.allInventory()).map(([id, inv]) => {
    const it = itemById(+id);
    return {
      id: it.id, name: it.name, name_zh: it.name_zh, icon: it.icon, type: it.type,
      effect: it.effect, is_permanent: it.is_permanent,
      equipped: !!inv.equipped, quantity: inv.quantity,
    };
  }) };
}
function equipItem(itemId) {
  const it = itemById(itemId);
  if (!it || !it.is_permanent) return { ok: false, message: "该装备不可装备" };
  const inv = Store.getInvItem(itemId);
  if (!inv) return { ok: false, message: "你还没有这件装备" };
  Store.setInvItem(itemId, { equipped: inv.equipped ? 0 : 1, quantity: inv.quantity });
  return { ok: true, message: inv.equipped ? "已卸下:" + it.name_zh : "已装备:" + it.name_zh };
}
function equipSkin(itemId) {
  const it = itemById(itemId);
  if (!it || it.type !== "skin") return { ok: false, message: "该商品不是皮肤", avatar: null };
  const key = SKIN_KEYS[it.name];
  if (!key) return { ok: false, message: "未知皮肤", avatar: null };
  const inv = Store.getInvItem(itemId);
  if (!inv) return { ok: false, message: "还没有购买这款皮肤", avatar: null };
  const u = Store.getUser();
  if (u.avatar === key) {
    Store.patchUser({ avatar: "adventurer" });
    return { ok: true, message: "已卸下皮肤,穿回布衣", avatar: "adventurer" };
  }
  Store.patchUser({ avatar: key });
  return { ok: true, message: "已穿戴:" + it.name_zh, avatar: key };
}
function useConsumable(itemId) {
  const it = itemById(itemId);
  if (!it || it.is_permanent) return { ok: false, message: "该道具不可消耗" };
  const inv = Store.getInvItem(itemId);
  if (!inv || inv.quantity < 1) return { ok: false, message: "库存不足" };
  if (inv.quantity <= 1) Store.removeInvItem(itemId);
  else Store.setInvItem(itemId, { equipped: inv.equipped, quantity: inv.quantity - 1 });
  return { ok: true, message: "已使用:" + it.name_zh };
}
function userHasItem(name) {
  for (const it of ITEMS) {
    if (it.name !== name) continue;
    const inv = Store.getInvItem(it.id);
    if (inv && inv.equipped) return true;
  }
  return false;
}
function useConsumableIfHas(name) {
  const it = ITEMS.find((i) => i.name === name);
  if (!it || it.is_permanent) return false;
  const inv = Store.getInvItem(it.id);
  if (!inv || inv.quantity < 1) return false;
  if (inv.quantity <= 1) Store.removeInvItem(it.id);
  else Store.setInvItem(it.id, { equipped: inv.equipped, quantity: inv.quantity - 1 });
  return true;
}

// ═══════════ 每日任务(game/tasks.py) ═══════════
const DAILY_GOALS = { monsters_killed: 10, words_learned: 15, words_reviewed: 8, max_combo: 5 };
function getTaskProgress() {
  const d = Store.getDaily();
  return {
    goals: DAILY_GOALS,
    progress: { monsters_killed: d.monsters_killed, words_learned: d.words_learned, words_reviewed: d.words_reviewed, max_combo: d.max_combo },
    completed: !!d.completed,
  };
}
function recordKill(wordsLearned = 0, wordsReviewed = 0) {
  Store.bumpDaily({ monsters_killed: 1, words_learned: wordsLearned, words_reviewed: wordsReviewed });
  const d = Store.getDaily();
  if (d.monsters_killed >= DAILY_GOALS.monsters_killed && d.words_learned >= DAILY_GOALS.words_learned && d.max_combo >= DAILY_GOALS.max_combo) {
    Store.setDailyCompleted();
    return true;
  }
  return false;
}

// ═══════════ 内存会话(测评/战斗/词藏练习) ═══════════
let _assessSess = null;   // {words, results, idx}
let _battleSess = null;   // {active, monster, combo, player_hp, kill_count, region_idx, boss_fight}
let _practiceSess = null; // {mode, words, idx}

// ═══════════ profile / 设置 ═══════════
function profilePayload() {
  const u = Store.getUser();
  const xpNext = xpToNext(u.level);
  const inLevel = Math.max(0, u.xp - xpForLevel(u.level));
  return {
    id: u.id, username: u.username, nickname: u.nickname, avatar: u.avatar,
    target_score: u.target_score, daily_minutes: u.daily_minutes, theme: u.theme,
    onboarded: !!u.onboarded,
    level: u.level, xp: u.xp,
    xp_in_level: inLevel, xp_to_next: xpNext,
    xp_percent: Math.round(Math.min(100, inLevel / xpNext * 100)),
    gold: u.gold, hp: u.hp, max_hp: u.max_hp, attack: u.attack,
  };
}

// ═══════════ 测评流程 ═══════════
function assessmentQuestion() {
  const sess = _assessSess;
  if (!sess) throw new Error("请先开始测评");
  if (sess.idx >= sess.words.length) return { done: true };
  const word = sess.words[sess.idx];
  const [options, correct] = buildOptions(word);
  return {
    idx: sess.idx, total: sess.words.length, word: word.word,
    options, answer: correct, difficulty: word.difficulty, done: false,
  };
}
function assessmentResult() {
  if (!_assessSess || !_assessSess.results.length) throw new Error("还没有测评数据");
  const profile = evaluateAssessment(_assessSess.results);
  const lv = profile.initial_level;
  const bonusHp = (lv - 1) * 10, bonusAtk = (lv - 1) * 1;
  const u = Store.getUser();
  const unlock = Math.max(u.unlocked_region, profile.unlock_region);
  Store.patchUser({
    level: lv, xp: xpForLevel(lv), max_hp: 100 + bonusHp, hp: 100 + bonusHp,
    attack: 10 + bonusAtk, unlocked_region: unlock, onboarded: 1,
  });
  const east = u.theme === "east";
  const unlockNames = [];
  for (let i = 0; i <= unlock; i++) unlockNames.push(east ? THEME_REGION_NAMES.east[i] : MAP_REGIONS[i].name);
  return {
    level: lv, correct_count: profile.correct_count, total: profile.total,
    unlock_region: unlock, unlock_regions: unlockNames,
    target_score: u.target_score, daily_minutes: u.daily_minutes,
  };
}

// ═══════════ 战斗流程 ═══════════
function battleAnswer(data) {
  if (!_battleSess || !_battleSess.monster) throw new Error("请先开始战斗");
  const correct = !!data.correct;
  const wordId = parseInt(data.word_id, 10) || 0;
  const responseTime = parseFloat(data.response_time) || 0;
  const difficulty = Math.max(1, Math.min(4, parseInt(data.difficulty, 10) || 2));
  const b = _battleSess;
  const u = Store.getUser();

  Store.recordBattle(wordId, correct, difficulty);
  recordAnswer(wordId, correct);

  const hasBerserker = userHasItem("berserker_blade");
  const hasMemorySword = userHasItem("memory_sword");
  const result = evaluateAnswer(correct, b.combo, u.level, u.attack, hasBerserker);
  let xpGained = 0, goldGained = 0, monsterDefeated = false, bossDefeated = false;
  const response = { correct, combo: result.combo, damage: result.damage };

  if (correct) {
    b.combo = result.combo;
    b.monster.hp -= result.damage;
    xpGained = xpForCorrect(result.combo, difficulty, hasMemorySword);
    addXp(xpGained);
    Store.setDailyCombo(result.combo);
    if (b.monster.hp <= 0) {
      monsterDefeated = true;
      goldGained = goldForKill(b.monster.gold, result.combo);
      const xpKill = xpForKill(difficulty, result.combo);
      addGold(goldGained);
      addXp(xpKill);
      xpGained += xpKill;
      b.kill_count += 1;
      Store.addRegionKill(b.region_idx);
      recordKill(1, 0);
      if (b.boss_fight) {
        bossDefeated = true;
        b.boss_fight = false;
      } else {
        b.monster = combatMonster(difficulty, Store.getUser().theme);
      }
    }
  } else {
    b.combo = 0;
    if (useConsumableIfHas("guard_shield")) {
      response.player_hurt = 0;
      response.shield_used = true;
    } else {
      b.player_hp -= result.player_hurt;
      response.player_hurt = result.player_hurt;
      response.shield_used = false;
    }
  }

  const u2 = Store.getUser();
  Object.assign(response, {
    monster_hp: Math.max(0, b.monster.hp),
    monster_max_hp: b.monster.max_hp,
    player_hp: Math.max(0, b.player_hp),
    xp_gained: xpGained, gold_gained: goldGained,
    monster_defeated: monsterDefeated, boss_defeated: bossDefeated,
    player_defeated: b.player_hp <= 0,
    new_level: u2.level, xp: u2.xp, gold: u2.gold, player_level: u2.level,
  });
  if (monsterDefeated && !bossDefeated) {
    response.next_question = nextBattleQuestion(difficulty);
    response.next_monster = b.monster;
  } else if (bossDefeated) {
    b.monster = null;
    b.active = false;
  }
  return response;
}

// ═══════════ 词藏练习流程 ═══════════
function practiceQuestion() {
  const p = _practiceSess;
  const w = p.words[p.idx];
  const base = { idx: p.idx, total: p.words.length, mode: p.mode };
  if (p.mode === "unseen") {
    return { ...base, word: w.word, meaning: w.meaning, phonetic: w.phonetic || "", part_of_speech: w.part_of_speech || "", word_id: w.id };
  }
  const [options, correct] = buildOptions({ id: w.word_id, difficulty: w.difficulty, meaning: w.meaning });
  return { ...base, word: w.word, options, answer: correct, phonetic: w.phonetic || "", part_of_speech: w.part_of_speech || "", word_id: w.word_id, difficulty: w.difficulty };
}
function practiceAnswer(data) {
  if (!_practiceSess) throw new Error("请先开始练习");
  if (_practiceSess.idx >= _practiceSess.words.length) {
    _practiceSess = null;
    throw new Error("本次练习已完成,请重新开始");
  }
  const correct = !!data.correct;
  const responseTime = parseFloat(data.response_time) || 0;
  const mode = _practiceSess.mode;
  const w = _practiceSess.words[_practiceSess.idx];
  const wordId = mode === "unseen" ? w.id : w.word_id;
  if (data.word_id != null && String(data.word_id) !== String(wordId)) throw new Error("题目不匹配,请刷新练习");

  let rec = null;
  if (mode === "unseen") {
    markSeen(wordId);
    recordKill(1, 0);
  } else {
    Store.recordBattle(wordId, correct, 2);
    rec = recordAnswer(wordId, correct);
    recordKill(0, 1);
  }
  _practiceSess.idx += 1;
  const done = _practiceSess.idx >= _practiceSess.words.length;
  if (done) _practiceSess = null;
  const resp = { ok: true, done, correct };
  if (rec) { resp.mastery = rec.mastery; resp.status = rec.status; }
  if (!done) resp.question = practiceQuestion();
  return resp;
}

// ═══════════ 成长统计 ═══════════
function statsPayload() {
  const b = Store.getBattles();
  const accuracy = b.total ? Math.round(b.correct / b.total * 100) : 0;
  const uws = Store.allUserWords();
  const mastered = uws.filter((r) => r.correct_count >= 2 && r.wrong_count === 0).length;
  const learning = uws.length; // status 永远不是 'new'(record_answer/mark_seen 都写 real status)
  const mastery = {};
  for (let d = 1; d <= 4; d++) {
    const dd = b.by_diff[d] || { t: 0, r: 0 };
    mastery[d] = dd.t ? Math.round(dd.r / dd.t * 100) : 0;
  }
  return { total_battles: b.total, accuracy, learned_words: learning, mastered_words: mastered, mastery };
}

// ═══════════ api() 兼容路由 ═══════════
async function localApi(path, opts = {}) {
  const method = (opts.method || "GET").toUpperCase();
  const data = opts.body ? JSON.parse(opts.body) : {};
  // 前端会把查询参数拼进 path(如 /api/vault/words?status=all&page=1);先剥掉再精确匹配,
  // 否则带 ? 的请求全掉进"未知接口"。_qs() 仍用原始 path 解析参数。
  const cleanPath = path.split("?")[0];

  // ── 用户 / 设置 ──
  if (cleanPath === "/api/profile" && method === "GET") return profilePayload();
  if (cleanPath === "/api/onboarding") {
    const u = Store.getUser();
    Store.patchUser({
      nickname: data.nickname || u.nickname,
      target_score: parseInt(data.target_score, 10) || u.target_score,
      daily_minutes: parseInt(data.daily_minutes, 10) || u.daily_minutes,
    });
    return { ok: true, user_id: u.id };
  }
  if (cleanPath === "/api/settings" && method === "PATCH") {
    const updates = {};
    if ("nickname" in data) {
      const nick = String(data.nickname || "").trim();
      if (!(nick.length >= 1 && nick.length <= 12)) throw new Error("昵称需 1-12 个字符");
      updates.nickname = nick;
    }
    if ("avatar" in data) {
      const av = data.avatar;
      if (typeof av !== "string" || !av.endsWith(".svg")) throw new Error("头像格式错误");
      updates.avatar = av;
    }
    if ("target_score" in data) {
      const ts = parseInt(data.target_score, 10);
      if (![425, 500, 550, 600].includes(ts)) throw new Error("目标分可选 425/500/550/600");
      updates.target_score = ts;
    }
    if ("daily_minutes" in data) {
      const dm = parseInt(data.daily_minutes, 10);
      if (![10, 20, 30, 60].includes(dm)) throw new Error("每日分钟可选 10/20/30/60");
      updates.daily_minutes = dm;
    }
    if ("theme" in data) {
      if (!["western", "east"].includes(data.theme)) throw new Error("未知主题");
      updates.theme = data.theme;
    }
    if (!Object.keys(updates).length) throw new Error("没有可更新的字段");
    Store.patchUser(updates);
    return { ok: true, theme: Store.getUser().theme, message: "设置已保存" };
  }

  // ── 账号(网页版单机档,登录/注册仅作兼容,不再创建新档) ──
  if (cleanPath === "/api/auth/register") return { ok: true, message: "单机存档,无需账号", user_id: 1 };
  if (cleanPath === "/api/auth/login") return { ok: true, message: "单机存档,无需账号", user_id: 1 };
  if (cleanPath === "/api/auth/logout") return { ok: true, message: "已退出" };
  if (cleanPath === "/api/auth/me") return { logged_in: false };

  // ── 测评 ──
  if (cleanPath === "/api/assessment/start") {
    if (Store.getUser().onboarded) throw new Error("已完成入坑测评,不能重复测评");
    const words = shuffle(pickAssessmentWords());
    _assessSess = { words, results: [], idx: 0 };
    return { total: words.length, started: true };
  }
  if (cleanPath === "/api/assessment/question" && method === "GET") return assessmentQuestion();
  if (cleanPath === "/api/assessment/answer") {
    if (!_assessSess) throw new Error("请先开始测评");
    const idx = data.idx;
    const correct = !!data.correct;
    const responseTime = parseFloat(data.response_time) || 0;
    const words = _assessSess.words;
    if (idx >= 0 && idx < words.length) {
      _assessSess.results.push({ word_id: words[idx].id, difficulty: words[idx].difficulty, correct, response_time: responseTime });
    }
    _assessSess.idx = idx + 1;
    return { ok: true, done: _assessSess.idx >= words.length };
  }
  if (cleanPath === "/api/assessment/result" && method === "GET") return assessmentResult();

  // ── 地图 ──
  if (cleanPath === "/api/map" && method === "GET") {
    const u = Store.getUser();
    const east = u.theme === "east";
    const regions = MAP_REGIONS.map((r, i) => {
      const unlocked = i === 0 || i <= u.unlocked_region
        || (i - 1 >= 0 && Store.getRegionKills(i - 1) >= REGION_KILL_TARGETS[i - 1]);
      return {
        ...r,
        name: east ? THEME_REGION_NAMES.east[i] : r.name,
        monster: themedMonsterName(u.theme, r.monster),
        unlocked,
        kills: Store.getRegionKills(i),
        kill_target: i < REGION_KILL_TARGETS.length ? REGION_KILL_TARGETS[i] : null,
      };
    });
    return { player_level: u.level, theme: u.theme, regions };
  }

  // ── 战斗 ──
  if (cleanPath === "/api/battle/start") {
    const difficulty = Math.max(1, Math.min(4, parseInt(data.difficulty, 10) || 2));
    const regionIdx = Math.max(0, Math.min(MAP_REGIONS.length - 1, parseInt(data.region_idx, 10) || 0));
    if (!regionUnlocked(regionIdx)) throw new Error("该区域尚未解锁");
    const u = Store.getUser();
    _battleSess = {
      active: true, monster: combatMonster(difficulty, u.theme), combo: 0,
      player_hp: u.hp, kill_count: 0, region_idx: regionIdx, boss_fight: false,
    };
    return { monster: _battleSess.monster, player_hp: u.hp, max_hp: u.max_hp, question: nextBattleQuestion(difficulty) };
  }
  if (cleanPath === "/api/battle/answer") return battleAnswer(data);
  if (cleanPath === "/api/battle/boss") {
    if (!_battleSess || !_battleSess.active || !_battleSess.monster) throw new Error("请先开始战斗");
    const difficulty = _battleSess.monster.difficulty;
    _battleSess.monster = combatMonster(difficulty, Store.getUser().theme, true, _battleSess.region_idx);
    _battleSess.boss_fight = true;
    return { monster: _battleSess.monster, question: nextBattleQuestion(difficulty) };
  }
  if (cleanPath === "/api/battle/question" && method === "GET") {
    if (!_battleSess || !_battleSess.monster) throw new Error("请先开始战斗");
    return nextBattleQuestion(_battleSess.monster.difficulty);
  }
  if (cleanPath === "/api/battle/end") {
    if (_battleSess) { _battleSess.active = false; _battleSess.boss_fight = false; }
    return { ok: true };
  }

  // ── 词藏 ──
  if (cleanPath === "/api/vault/stats" && method === "GET") return vaultStats();
  if (cleanPath === "/api/vault/words" && method === "GET") {
    const status = data && data.status !== undefined ? data.status : _qs(path, "status", "all");
    const statuses = ["unseen", "weak", "learning", "mastered"];
    if (!statuses.includes(status) && status !== "all") throw new Error("未知状态");
    return searchWords(
      status,
      _qs(path, "q", "").trim(),
      parseInt(_qs(path, "page", 1), 10),
      parseInt(_qs(path, "size", 20), 10),
    );
  }
  if (cleanPath.indexOf("/api/vault/word/") === 0 && method === "GET") {
    const id = parseInt(path.slice("/api/vault/word/".length), 10);
    const w = wordDetail(id);
    if (!w) throw new Error("词不存在");
    return w;
  }
  if (cleanPath === "/api/vault/practice") {
    const mode = data.mode;
    if (!["weak", "review", "unseen"].includes(mode)) throw new Error("未知模式");
    let words;
    if (mode === "weak") words = weakWords(50);
    else if (mode === "review") words = dueReview(50);
    else words = unseenWords(50);
    if (!words.length) return { error: "这个模式下还没有可练的词", empty: true };
    _practiceSess = { mode, words, idx: 0 };
    return { started: true, mode, total: words.length, question: practiceQuestion() };
  }
  if (cleanPath === "/api/vault/answer") return practiceAnswer(data);

  // ── 每日任务 ──
  if (cleanPath === "/api/tasks" && method === "GET") return getTaskProgress();

  // ── 商店 / 背包 / 皮肤 ──
  if (cleanPath === "/api/shop" && method === "GET") return getShop();
  if (cleanPath === "/api/shop/buy") return buyItem(parseInt(data.item_id, 10));
  if (cleanPath === "/api/inventory" && method === "GET") return getInventory();
  if (cleanPath === "/api/inventory/equip") return equipItem(parseInt(data.item_id, 10));
  if (cleanPath === "/api/inventory/use") return useConsumable(parseInt(data.item_id, 10));
  if (cleanPath === "/api/skin/equip") return equipSkin(parseInt(data.item_id, 10));

  // ── 成长 ──
  if (cleanPath === "/api/stats" && method === "GET") return statsPayload();

  throw new Error(`未知接口:${path}`);
}

// 从 path 的 query string 取参数(?status=..&page=..)
function _qs(path, key, fallback) {
  const i = path.indexOf("?");
  if (i < 0) return fallback;
  const params = new URLSearchParams(path.slice(i));
  return params.get(key) != null ? params.get(key) : fallback;
}
