/* 单词远征 · 四级背词 RPG —— 前端逻辑(单页多视图) */

const $ = (id) => document.getElementById(id);

// 显示层:单词首字母大写、其余小写(China/OK/iPhone 等专有名词、缩写保持原样)
const titleCase = (w) =>
  w && /[A-Z]/.test(w.slice(1)) ? w : w.charAt(0).toUpperCase() + w.slice(1).toLowerCase();

// ═══════════ 美术资源映射(纯展示层,主题感知) ═══════════
const ASSET = "assets";
let theme = "western"; // 全局主题:western 西幻王国 / east 东方山海
// 各主题怪物中文名 → slug(战斗怪名由后端按主题给出)
const MONSTER_SLUG_W = {
  "史莱姆": "slime",
  "哥布林": "goblin",
  "狼人": "werewolf",
  "恶魔": "demon",
  "四级龙": "dragon",
  "哥布林王": "goblin_king",       // 章节 Boss(区域0)
  "哥布林大酋长": "goblin_war_chief", // 区域1
  "哥布林国王": "goblin_high_king", // 区域2
  "狼王": "wolf_king",             // 区域3
  "深渊魔王": "demon_lord",        // 区域4
  "四级龙王": "dragon_emperor",    // 区域5
};
const MONSTER_SLUG_E = {
  "草木小妖": "grass_spirit",
  "青竹狐妖": "fox_fairy",
  "苍狼妖": "wolf_demon",
  "赤魇": "mountain_ghost",
  "深渊蛟龙": "flood_dragon",
  "草木妖王": "grass_king",        // 章节 Boss 东方化身(区域0)
  "青竹狐王": "fox_king",          // 区域1
  "狐妖大君": "fox_sovereign",     // 区域2
  "苍狼王": "wolf_king",           // 区域3
  "炎魔大君": "flame_fiend",       // 区域4
  "深渊龙皇": "abyss_dragon",      // 区域5
};
// 场景(按区域索引)→ 各主题 slug
const SCENE_SLUG_W = ["grassland", "forest", "fort", "wasteland", "volcano", "dragonnest"];
const SCENE_SLUG_E = ["paddy", "bamboo", "gate", "wild", "lava", "abyss"];
// 头像 key → 各主题角色 slug(未收录的头像回落到各主题默认角色)
const PLAYER_SLUG_W = { adventurer: "adventurer", knight: "knight", warrior: "warrior", mage: "mage" };
const PLAYER_SLUG_E = { adventurer: "xia", xia: "xia", knight: "hero", warrior: "daoist", mage: "scholar" };
const slugOf = (name) => MONSTER_SLUG_W[name] || MONSTER_SLUG_E[name] || "slime";
const monsterImg = (name, state = "idle") => {
  const sub = MONSTER_SLUG_E[name] ? "east/" : "";
  return `${ASSET}/${sub}monsters/monster_${slugOf(name)}_${state}.svg`;
};
// 主题资源路径解析:east 主题自动加 east/ 前缀(西幻为根目录)
const themeAsset = (rel) => `${ASSET}/${theme === "east" ? "east/" : ""}${rel}`;
const sceneImg = (idx) => {
  const slugs = theme === "east" ? SCENE_SLUG_E : SCENE_SLUG_W;
  return themeAsset(`scenes/scene_${slugs[idx]}.svg`);
};
const playerSlug = () => {
  const key = (user && user.avatar) || "adventurer";
  const map = theme === "east" ? PLAYER_SLUG_E : PLAYER_SLUG_W;
  return map[key] || (theme === "east" ? "xia" : "adventurer");
};
const PLAYER_IMG = (state) => themeAsset(`characters/character_${playerSlug()}_${state}.svg`);
const ITEM_IMG = {
  memory_sword: `${ASSET}/icons/icon_sword.svg`,
  echo_earring: `${ASSET}/icons/icon_earring.svg`,
  insight_eye: `${ASSET}/icons/icon_eye.svg`,
  etymology_book: `${ASSET}/icons/icon_book.svg`,
  guard_shield: `${ASSET}/icons/icon_shield.svg`,
  berserker_blade: `${ASSET}/icons/icon_sword.svg`,
};
const itemImg = (name) => ITEM_IMG[name] || `${ASSET}/icons/icon_potion.svg`;
// 皮肤商品 → 各主题皮肤 slug(w 西幻 / e 东方)。user.avatar 存的是 w 侧 key
const SKIN_ROLE = {
  knight_skin: { w: "knight", e: "hero" },
  warrior_skin: { w: "warrior", e: "daoist" },
  mage_skin: { w: "mage", e: "scholar" },
};
const skinKeyOf = (name) => (SKIN_ROLE[name] || {}).w || "";
// 皮肤预览图:当前主题下该皮肤的 idle 形象
const skinImg = (name) => {
  const role = SKIN_ROLE[name];
  if (!role) return "";
  const slug = theme === "east" ? role.e : role.w;
  return themeAsset(`characters/character_${slug}_idle.svg`);
};

// ═══════════ 战斗表现(纯展示) ═══════════
let monsterStateTimer = null;
function setMonsterState(state, revertMs = 0) {
  if (!battle.active || !battle.monster) return;
  const img = $("battle-monster").querySelector("img");
  if (!img) return;
  img.src = monsterImg(battle.monster.name, state);
  if (revertMs > 0) {
    clearTimeout(monsterStateTimer);
    monsterStateTimer = setTimeout(() => {
      const im = $("battle-monster").querySelector("img");
      if (im) im.src = monsterImg(battle.monster.name, "idle");
    }, revertMs);
  }
}
function setPlayerState(state) {
  const champ = document.querySelector(".player-champ");
  const img = champ ? champ.querySelector("img") : null;
  if (img) img.src = PLAYER_IMG(state);
  if (!champ) return;
  champ.classList.remove("swing");
  if (state === "attack") {
    void champ.offsetWidth; // 强制重排,重启动画
    champ.classList.add("swing");
  }
}
function playSlash() {
  const el = $("battle-slash");
  if (!el) return;
  el.classList.remove("show");
  void el.offsetWidth;
  el.classList.add("show");
}
// 剑气:从角色(下方)斜向上飞向怪物(上方),命中后回调 onHit。
// 手动 rAF 插值(非 CSS 动画),飞行轨迹可控、命中时机精确。
function playBeam(onHit) {
  const box = $("battle-box");
  const monster = $("battle-monster");
  const beam = $("battle-beam");
  const champ = document.querySelector(".player-champ");
  if (!box || !monster || !beam || !champ) return;
  const br = box.getBoundingClientRect();
  const mr = monster.getBoundingClientRect();
  const cr = champ.getBoundingClientRect();
  const sx = cr.left + cr.width / 2 - br.left; // 起点:角色顶部中心
  const sy = cr.top - br.top;
  const ex = mr.left + mr.width / 2 - br.left; // 终点:怪物中心偏上
  const ey = mr.top + mr.height * 0.3 - br.top;
  const ang = (Math.atan2(ey - sy, ex - sx) * 180) / Math.PI;
  const DUR = 300; // 飞行时长 ms
  const t0 = performance.now();
  beam.style.transform = `translate(${sx}px, ${sy}px) rotate(${ang}deg)`;
  beam.style.opacity = "1";
  const step = (now) => {
    const p = Math.min(1, (now - t0) / DUR);
    const e = p * p; // ease-in:越接近怪物越快
    const cx = sx + (ex - sx) * e;
    const cy = sy + (ey - sy) * e;
    beam.style.transform = `translate(${cx}px, ${cy}px) rotate(${ang}deg)`;
    beam.style.opacity = String(1 - e);
    if (p < 1) {
      requestAnimationFrame(step);
    } else {
      beam.style.opacity = "0"; // 抵达怪物,渐隐
      if (onHit) onHit();
    }
  };
  requestAnimationFrame(step);
}
function burstSparkle() {
  const el = $("battle-sparkle");
  if (!el) return;
  el.classList.remove("show");
  void el.offsetWidth;
  el.classList.add("show");
}
function shakeScreen() {
  const b = $("battle-box");
  if (!b) return;
  b.classList.remove("shake");
  void b.offsetWidth;
  b.classList.add("shake");
}

// ═══════════ 音效引擎(纯 Web Audio 合成,零外部文件,完全离线) ═══════════
let _actx = null, _noiseBuf = null, _masterGain = null;
function audioCtx() {
  if (!_actx) {
    const AC = window.AudioContext || window.webkitAudioContext;
    if (!AC) return null;
    _actx = new AC();
    _masterGain = _actx.createGain();
    _masterGain.gain.value = 0.42;
    _masterGain.connect(_actx.destination);
  }
  if (_actx.state === "suspended") _actx.resume();
  return _actx;
}
function noiseBuf(ctx) {
  if (!_noiseBuf) {
    _noiseBuf = ctx.createBuffer(1, Math.floor(ctx.sampleRate * 0.5), ctx.sampleRate);
    const d = _noiseBuf.getChannelData(0);
    for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
  }
  return _noiseBuf;
}
// 扫频音:任意波形振荡器,自动过主增益(所有音效共用一个输出,防爆音)
function sTone(type, f0, f1, dur, vol) {
  const ctx = audioCtx();
  if (!ctx) return;
  const t = ctx.currentTime;
  const o = ctx.createOscillator();
  o.type = type;
  o.frequency.setValueAtTime(f0, t);
  o.frequency.exponentialRampToValueAtTime(Math.max(24, f1), t + dur);
  const g = ctx.createGain();
  g.gain.setValueAtTime(vol, t);
  g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
  o.connect(g); g.connect(_masterGain);
  o.start(t); o.stop(t + dur + 0.03);
}
// 噪声爆发(带通滤波):打击的"啪/崩"质感
function sNoise(dur, freq, q, vol) {
  const ctx = audioCtx();
  if (!ctx) return;
  const t = ctx.currentTime;
  const src = ctx.createBufferSource();
  src.buffer = noiseBuf(ctx);
  const f = ctx.createBiquadFilter();
  f.type = "bandpass"; f.frequency.value = freq; f.Q.value = q;
  const g = ctx.createGain();
  g.gain.setValueAtTime(vol, t);
  g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
  src.connect(f); f.connect(g); g.connect(_masterGain);
  src.start(t); src.stop(t + dur + 0.03);
}

// ═══ 真实音效(录音素材,离线内置) ═══
// 每只怪按身份用真实生物音效(受击/暴击/击杀),远比合成音色生动。
// 素材来自 Mixkit(Mixkit License:免费商用、无需署名)。mp3 内置于 /static/assets/sfx/。
// 启动后预加载解码为 AudioBuffer;播放失败(未加载完/缺文件)时回退到下方合成音色。
const SFX_FILES = {
  slime:    { hit: "slime_hit.mp3",    crit: "slime_crit.mp3",    kill: "slime_kill.mp3" },    // 果冻啵声
  goblin:   { hit: "goblin_hit.mp3",   crit: "goblin_crit.mp3",   kill: "goblin_kill.mp3" },   // 小妖吱叫
  werewolf: { hit: "wolf_hit.mp3",     crit: "wolf_crit.mp3",     kill: "wolf_kill.mp3" },     // 狼嚎
  demon:    { hit: "demon_hit.mp3",    crit: "demon_crit.mp3",    kill: "demon_kill.mp3" },    // 深渊轰鸣
  dragon:   { hit: "dragon_hit.mp3",   crit: "dragon_crit.mp3",   kill: "dragon_kill.mp3" },   // 龙吼
};
const SFX_SLUG_ALIAS = {
  grass_spirit: "slime", fox_fairy: "goblin", wolf_demon: "werewolf", mountain_ghost: "demon", flood_dragon: "dragon",
  goblin_king: "goblin", grass_king: "goblin",
  goblin_war_chief: "goblin", goblin_high_king: "goblin", fox_king: "goblin", fox_sovereign: "goblin",
  wolf_king: "werewolf",
  demon_lord: "demon", flame_fiend: "demon",
  dragon_emperor: "dragon", abyss_dragon: "dragon",
};
// 真实素材时长上限(秒):受击/击杀要"短促",素材却拖了几秒长尾巴 → 只播前段、末尾渐隐。
// 按身份 slug + 种类配置;未列出的种类播放完整素材。
const SFX_MAX_DUR = {
  slime:   { hit: 0.45 },             // 史莱姆:6.5s 太长,截成短促"啵"
  goblin:  { kill: 1.2 },             // 哥布林击杀音 12.8s,截短
  werewolf:{ crit: 2.0, kill: 1.8 },  // 狼:暴击 3.2s / 击杀 8.1s,都收紧
  demon:   { hit: 1.2, crit: 2.0, kill: 1.8 },   // 恶魔:受击/暴击/击杀都长
  dragon:  { hit: 1.2, crit: 2.0, kill: 2.2 },   // 龙:全收紧(击杀 12.9s)
};
const _sfxBufs = {}; // "slug_kind" → AudioBuffer
let _sfxLoading = false;
// 预加载:抓全部 mp3 → decodeAudioData → 缓存。任一个失败只跳过,不影响游戏。
function preloadSfx() {
  if (_sfxLoading) return;
  _sfxLoading = true;
  for (const [slug, kinds] of Object.entries(SFX_FILES)) {
    for (const [kind, file] of Object.entries(kinds)) {
      const url = `${ASSET}/sfx/${file}`;
      fetch(url)
        .then((r) => (r.ok ? r.arrayBuffer() : Promise.reject(new Error(`${file} ${r.status}`))))
        .then((buf) => {
          const ctx = audioCtx();
          if (!ctx) throw new Error("no audio ctx");
          return ctx.decodeAudioData(buf);
        })
        .then((ab) => { _sfxBufs[`${slug}_${kind}`] = ab; })
        .catch(() => { /* 合成音色兜底,无需告警 */ });
    }
  }
}
// 播放一段真实音效:可变速(0.8 慢重 / 1.0 原速 / 1.2 快脆)。
// maxDur > 0 时若素材更长,只播前 maxDur 秒(保留音高),末尾 40ms 渐隐防爆音。
function playSfxFile(slug, kind, playbackRate = 1, maxDur = 0) {
  let ab = _sfxBufs[`${slug}_${kind}`];
  const ctx = audioCtx();
  if (!ab || !ctx) return false;
  if (maxDur > 0 && ab.duration > maxDur) {
    const ch = ab.numberOfChannels;
    const n = Math.floor(ctx.sampleRate * maxDur);
    const fade = Math.min(n, Math.floor(ctx.sampleRate * 0.04));
    const nb = ctx.createBuffer(ch, n, ctx.sampleRate);
    for (let c = 0; c < ch; c++) {
      const s = ab.getChannelData(c), o = nb.getChannelData(c);
      for (let i = 0; i < n; i++) {
        let v = s[i];
        if (i > n - fade) v *= (n - i) / fade;
        o[i] = v;
      }
    }
    ab = nb;
  }
  const src = ctx.createBufferSource();
  src.buffer = ab;
  src.playbackRate.value = playbackRate;
  const g = ctx.createGain();
  g.gain.value = 0.9; // 与合成音效同路,经主增益
  src.connect(g); g.connect(_masterGain);
  src.start();
  return true;
}

// ═══ 怪物身份音效:每只怪被打/被击杀,声音都像它自己 ═══
// 史莱姆→果冻啵声 · 哥布林→小妖吱叫 · 狼人→狼嚎 · 恶魔→深渊轰鸣 · 龙→龙吼
// 两个主题共用同一套身份:东方怪(草木小妖/青竹狐妖/苍狼妖/赤魇/深渊蛟龙)
// 按 VOICE_ALIAS 映射到对应西幻身份,只做展示层名字不同、音色一致。
const MONSTER_VOICE = {
  slime: {
    // 果冻:湿软的"啵",低频水感
    hit() { sTone("sine", 160, 70, 0.10, 0.9); sTone("triangle", 600, 220, 0.06, 0.35); },
    // 暴击:果冻被劈开,啵得更大、带水花
    crit() { sTone("sine", 180, 60, 0.16, 1.0); sTone("triangle", 720, 160, 0.10, 0.5); sNoise(0.05, 2600, 1.2, 0.3); },
    // 死亡:果冻炸开、漏气塌下去
    kill() { sTone("sine", 240, 45, 0.35, 0.9); sTone("triangle", 420, 90, 0.25, 0.5); sNoise(0.12, 900, 0.8, 0.4); },
  },
  goblin: {
    // 小妖:尖细的"吱",木屑似的脆响
    hit() { sTone("sawtooth", 240, 100, 0.08, 0.45); sTone("square", 1000, 500, 0.05, 0.2); },
    crit() { sTone("sawtooth", 300, 120, 0.12, 0.55); sTone("square", 1200, 400, 0.08, 0.3); sNoise(0.05, 3200, 1.0, 0.25); },
    kill() { sTone("square", 700, 140, 0.20, 0.4); sTone("sawtooth", 200, 80, 0.18, 0.4); },
  },
  werewolf: {
    // 狼人:低吼 + 毛皮闷响
    hit() { sTone("sawtooth", 130, 55, 0.14, 0.6); sNoise(0.09, 350, 1.0, 0.45); },
    crit() { sTone("sawtooth", 140, 45, 0.20, 0.7); sNoise(0.12, 320, 1.0, 0.5); sTone("triangle", 260, 120, 0.08, 0.3); },
    // 死亡:狼嚎——上扬的长嚎
    kill() { sTone("sine", 260, 1000, 0.55, 0.6); sTone("sine", 520, 2000, 0.50, 0.28); sNoise(0.18, 500, 1.0, 0.3); },
  },
  demon: {
    // 恶魔:深渊轰鸣 + 高频刺耳
    hit() { sTone("sawtooth", 90, 40, 0.18, 0.7); sNoise(0.12, 220, 1.0, 0.5); },
    crit() { sTone("sawtooth", 95, 35, 0.25, 0.8); sNoise(0.16, 200, 1.0, 0.55); sTone("triangle", 1200, 400, 0.06, 0.2); },
    kill() { sTone("sawtooth", 70, 25, 0.6, 0.8); sTone("triangle", 150, 60, 0.4, 0.4); sNoise(0.3, 180, 1.0, 0.5); },
  },
  dragon: {
    // 龙:龙鳞重击 + 低沉龙吼
    hit() { sTone("sawtooth", 65, 30, 0.22, 0.8); sNoise(0.16, 160, 1.2, 0.6); sTone("triangle", 320, 120, 0.07, 0.25); },
    crit() { sTone("sawtooth", 70, 28, 0.30, 0.9); sNoise(0.22, 150, 1.2, 0.65); sTone("triangle", 340, 100, 0.12, 0.3); },
    // 死亡:龙吼——超低长吼 + 气浪
    kill() { sTone("sawtooth", 80, 30, 0.9, 1.0); sTone("sine", 45, 28, 0.8, 0.9); sNoise(0.45, 120, 1.2, 0.6); sTone("triangle", 200, 80, 0.35, 0.3); },
  },
};
// 东方怪 → 身份别名(与西幻共用音色)
const VOICE_ALIAS = {
  grass_spirit: "slime", fox_fairy: "goblin", wolf_demon: "werewolf",
  mountain_ghost: "demon", flood_dragon: "dragon",
  goblin_king: "goblin", grass_king: "goblin",
  goblin_war_chief: "goblin", goblin_high_king: "goblin", fox_king: "goblin", fox_sovereign: "goblin",
  wolf_king: "werewolf",
  demon_lord: "demon", flame_fiend: "demon",
  dragon_emperor: "dragon", abyss_dragon: "dragon",
};
const voiceOf = (name) => {
  const key = VOICE_ALIAS[slugOf(name)] || slugOf(name);
  return MONSTER_VOICE[key] || MONSTER_VOICE.slime;
};

// 统一入口:答对受击(普通/暴击)、击杀——优先真实音效,未加载完则合成兜底
const sfxBase = (name, kind, rate) => {
  const key = SFX_SLUG_ALIAS[slugOf(name)] || slugOf(name);
  const maxDur = (SFX_MAX_DUR[key] && SFX_MAX_DUR[key][kind]) || 0;
  if (playSfxFile(key, kind, rate, maxDur)) return; // 真实音效已播,无需合成
  voiceOf(name)[kind](); // 合成兜底
};
function sfxHit(name) { sfxBase(name, "hit", 1); }
function sfxCrit(name) { sfxBase(name, "crit", 0.9); }
function sfxMonsterKill(name) { sfxBase(name, "kill", 0.85); }
// 玩家被怪击中:下滑"闷哼"
function sfxPlayerHurt() {
  sTone("sawtooth", 200, 90, 0.28, 0.5);
  sTone("sine", 120, 60, 0.22, 0.4);
}
// 守护之盾格挡:金属"叮当"声
function sfxShield() {
  sTone("triangle", 1250, 900, 0.12, 0.35);
  sTone("triangle", 1750, 1300, 0.10, 0.22);
  sNoise(0.05, 5000, 1.2, 0.2);
}
// 玩家倒下:悲伤下滑
function sfxPlayerDefeat() {
  sTone("sawtooth", 260, 80, 0.6, 0.45);
  sTone("sine", 180, 55, 0.7, 0.5);
}
// Boss 登场怒吼:按身份放(哥布林吱叫 / 狼嚎 / 深渊轰鸣 / 龙吼),素材截短避免拖长
const BOSS_ROAR = {
  goblin_king: "goblin", goblin_war_chief: "goblin", goblin_high_king: "goblin",
  grass_king: "goblin", fox_king: "goblin", fox_sovereign: "goblin",
  wolf_king: "werewolf",
  demon_lord: "demon", flame_fiend: "demon",
  dragon_emperor: "dragon", abyss_dragon: "dragon",
};
function sfxBossRoar(bossKey) {
  const id = BOSS_ROAR[bossKey] || "goblin";
  const maxDur = (SFX_MAX_DUR[id] && SFX_MAX_DUR[id].kill) || 0;
  if (playSfxFile(id, "kill", 0.9, maxDur)) return;
  const v = MONSTER_VOICE[id] || MONSTER_VOICE.goblin;
  v.kill(); // 合成兜底:同身份的死亡咆哮
}
// 胜利小号:上扬三连音
function sfxVictory() {
  sTone("triangle", 660, 660, 0.14, 0.4);
  setTimeout(() => sTone("triangle", 880, 880, 0.14, 0.4), 140);
  setTimeout(() => sTone("triangle", 1100, 1100, 0.24, 0.45), 280);
}
// ── Boss 英语台词:原生 speechSynthesis(零外部 TTS) ──
// 每只 Boss 一个声音档案:统一选男声(en-US Guy/David…),pitch 越低越沉、rate 越慢越威严。
const BOSS_VOICE = {
  goblin_king:      { pitch: 0.55, rate: 0.82 }, // 粗嗓小大王
  goblin_war_chief: { pitch: 0.50, rate: 0.78 }, // 战阵凶酋
  goblin_high_king: { pitch: 0.46, rate: 0.74 }, // 老成国王
  wolf_king:        { pitch: 0.40, rate: 0.68 }, // 低沉狼吼
  demon_lord:       { pitch: 0.35, rate: 0.62 }, // 深渊沉鸣
  dragon_emperor:   { pitch: 0.30, rate: 0.56 }, // 最沉缓龙吼
};
// 东方 Boss 与对应西幻同声(同一身份,不同打扮)
const BOSS_VOICE_ALIAS = {
  grass_king: "goblin_king", fox_king: "goblin_war_chief", fox_sovereign: "goblin_high_king",
  flame_fiend: "demon_lord", abyss_dragon: "dragon_emperor",
};
const MALE_VOICE_RE = /guy|david|mark|james|brian|george|christopher|ryan|michael|daniel|eric|thomas|stephen|paul/i;
let _bossVoice = null;
function pickBossVoice() {
  if (_bossVoice) return _bossVoice;
  const vs = ("speechSynthesis" in window) ? speechSynthesis.getVoices() : [];
  if (!vs.length) return null;
  const en = vs.filter((v) => v.lang && v.lang.toLowerCase().startsWith("en"));
  const enUS = en.filter((v) => v.lang.toLowerCase().startsWith("en-us"));
  const male = (pool) => pool.find((v) => MALE_VOICE_RE.test(v.name));
  _bossVoice = male(enUS) || male(en) || enUS[0] || en[0] || null;
  return _bossVoice;
}
if ("speechSynthesis" in window) {
  speechSynthesis.addEventListener("voiceschanged", () => { _bossVoice = null; pickBossVoice(); }, { once: true });
}
// 读出 Boss 台词:用该 Boss 的语音档案
let _bossSpeakTimer = null;
function speakBossLine(en, bossKey) {
  if (!("speechSynthesis" in window)) return;
  speechSynthesis.cancel();
  clearTimeout(_bossSpeakTimer);
  const prof = BOSS_VOICE[BOSS_VOICE_ALIAS[bossKey] || bossKey] || BOSS_VOICE.goblin_king;
  const u = new SpeechSynthesisUtterance(en);
  u.lang = "en-US";
  u.rate = prof.rate;
  u.pitch = prof.pitch;
  const voice = pickBossVoice();
  if (voice) u.voice = voice;
  // Chromium 竞态:cancel() 后立即 speak() 可能把新台词吞掉(尤其前一句还在播时)。
  // 延迟一帧再 speak,新台词必定开始。
  _bossSpeakTimer = setTimeout(() => speechSynthesis.speak(u), 80);
}

// ═══════════ 单词发音:原生 speechSynthesis 优先,离线 meSpeak 兜底 ═══════════
// 部分手机浏览器/WebView(尤其 TapTap 内置、微信)没有 speechSynthesis,发音按钮会报
// "不支持"。无原生语音的设备在页面加载时即预载随包附带的 meSpeak(espeak 编译到 JS 的
// 纯前端离线 TTS),用户第一次点发音就直接出声;有原生语音的设备不预载,不白解析引擎。
// VENDOR 不带主题前缀(发音引擎与美术主题无关),web 构建时 ASSET 已相对化。
//
// 直连核心版(v1.0.3):不用 meSpeak 前端加载器。旧版靠它 document.currentScript.src
// 推导路径、再 XHR 拉语音 JSON,在 TapTap 内置 WebView/自定义协议下最易挂(报"离线引擎
// 不可用")。新版把整条链路全换成脚本标签 + 内存喂数据,零网络、零路径解析:
//   ① mespeak-xhr-shim.js —— 垫片:任何"语音 JSON"XHR 直接从内存全局应答
//   ② voices-en-us.js     —— 语音数据打进全局 __WQ_VOICE_JSON(script 标签,file:// 也行)
//   ③ mespeak-core.js     —— 引擎本体主线程直跑,loadVoice 经垫片取语音
const VENDOR = ASSET + "/vendor";
let _mespeakState = "idle"; // idle | loading | ready | failed

function _nativeSpeak(word) {
  if (!("speechSynthesis" in window) || !window.SpeechSynthesisUtterance) return false;
  try {
    const u = new SpeechSynthesisUtterance(word);
    u.lang = "en-US";
    u.rate = 0.9;
    // 显式挑英语音色:默认音色可能是中文,发音会走调甚至无声(Android 常见坑)
    const vs = speechSynthesis.getVoices();
    const enUS = vs.find((v) => v.lang && v.lang.toLowerCase().startsWith("en-us"));
    const en = enUS || vs.find((v) => v.lang && v.lang.toLowerCase().startsWith("en"));
    if (en) u.voice = en;
    speechSynthesis.speak(u);
    return true;
  } catch (e) {
    return false;
  }
}

function scriptAt(src) {
  return new Promise((res, rej) => {
    const s = document.createElement("script");
    s.src = src;
    s.onload = res;
    s.onerror = () => rej(new Error("script load fail: " + src));
    document.head.appendChild(s);
  });
}

function loadMeSpeak() {
  if (_mespeakState === "loading") return; // idle 与 failed 都允许(重新)拉起
  _mespeakState = "loading";
  const base = `${VENDOR}/mespeak`;
  scriptAt(`${base}/mespeak-xhr-shim.js`)
    .then(() => scriptAt(`${base}/voices-en-us.js`))
    .then(() => scriptAt(`${base}/mespeak-core.js`))
    .then(() => {
      try {
        if (!window.meSpeakCore || !window.__WQ_VOICE_JSON) { _mespeakState = "failed"; return; }
        // 垫片按后缀拦截这个 URL,直接返回内存里的语音 JSON,不发任何请求
        window.meSpeakCore.loadVoice("voices/en/en-us.json", (ok) => {
          _mespeakState = ok ? "ready" : "failed";
          if (!ok && window.console) console.warn("[speak] meSpeak voice load failed");
        });
      } catch (e) {
        _mespeakState = "failed";
      }
    })
    .catch(() => { _mespeakState = "failed"; });
}

// 共享音频上下文:在用户手势里创建/恢复,保证移动端 WebView 能出声
let _ttsCtx = null;
function _ensureTtsCtx() {
  try {
    if (!_ttsCtx) _ttsCtx = new (window.AudioContext || window.webkitAudioContext)();
    if (_ttsCtx.state === "suspended") _ttsCtx.resume();
    return _ttsCtx;
  } catch (e) { return null; }
}
["touchstart", "pointerdown", "click"].forEach((ev) =>
  document.addEventListener(ev, _ensureTtsCtx, { once: true, passive: true })
);

// meSpeakCore.speak 返回 WAV 的 ArrayBuffer,喂给 Web Audio 播放
function _playTtsWav(wav) {
  const ctx = _ensureTtsCtx();
  if (!ctx || !wav || !wav.byteLength) return false;
  try {
    const buf = wav instanceof ArrayBuffer ? wav : wav.buffer;
    ctx.decodeAudioData(buf, (audio) => {
      const src = ctx.createBufferSource();
      src.buffer = audio;
      src.connect(ctx.destination);
      src.start(0);
    }, () => {});
    return true;
  } catch (e) { return false; }
}

// 统一发音入口:返回 true 表示已出声/已排队出声
function speakWord(word) {
  if (!word) return false;
  if (_nativeSpeak(word)) return true;
  // 预载一般已就绪,点一下即出声;万一没就绪(极快进战斗)或上次加载失败,点击时重试
  if (_mespeakState === "idle" || _mespeakState === "failed") { loadMeSpeak(); toast("发音引擎加载中,马上就好", 1200); return false; }
  if (_mespeakState === "loading") { toast("发音引擎加载中,马上就好", 1200); return false; }
  if (_mespeakState === "ready" && window.meSpeakCore) {
    try {
      const argstack = ["-w", "wav.wav", "-a", "100", "-g", "0", "-p", "50", "-s", "160", "-b", "1", "-v", "en/en-us", "--path=espeak", word];
      const wav = window.meSpeakCore.speak(argstack);
      return _playTtsWav(wav);
    } catch (e) { return false; }
  }
  toast("当前环境不支持发音(无系统语音且离线引擎不可用)", 2000);
  return false;
}

// 预载:无原生语音的设备(尤其 TapTap 内置 WebView)从页面加载起就静默拉起离线引擎,
// 用户第一次点发音直接出声,不用等"再点一次"。有原生语音的设备不预载,不白解析引擎。
if (!("speechSynthesis" in window)) loadMeSpeak();

// ═══════════ 基础工具 ═══════════
async function api(path, opts = {}) {
  return localApi(path, opts);
}

function toast(msg, ms = 2200) {
  const old = document.querySelector(".toast");
  if (old) old.remove();
  const t = document.createElement("div");
  t.className = "toast";
  t.textContent = msg;
  document.body.appendChild(t);
  setTimeout(() => t.remove(), ms);
}

// 全局错误兜底:任何未捕获的异步失败/脚本异常都可见,不再静默卡死
window.addEventListener("unhandledrejection", (e) => {
  console.error("[unhandledrejection]", e.reason);
  toast("操作失败:" + (e.reason && e.reason.message ? e.reason.message : String(e.reason)), 3000);
});
window.addEventListener("error", (e) => {
  console.error("[error]", e.message);
});

function showView(id) {
  document.querySelectorAll(".view").forEach((v) => v.classList.remove("active"));
  const target = $("view-" + id);
  if (target) target.classList.add("active");
  // 单屏布局下滚动容器是 #views(顶部 HUD / 底部导航固定),滚它而不是整个页面
  const views = $("views");
  if (views) views.scrollTop = 0;
}

function renderHud(user) {
  $("hud-avatar").innerHTML = `<img src="${PLAYER_IMG("idle")}" alt="冒险者">`;
  $("hud-nick").textContent = user.nickname || "冒险者";
  $("hud-level").textContent = "Lv." + user.level;
  $("hud-hp").textContent = user.hp;
  $("hud-atk").textContent = user.attack;
  $("hud-gold").textContent = user.gold;
  $("hud-xp-fill").style.width = user.xp_percent + "%";
  $("hud-xp-text").textContent = `${user.xp_in_level}/${user.xp_to_next}`;
}

// ═══════════ 全局状态 ═══════════
let user = null;
let mapData = null;
let battle = {
  active: false,
  monster: null,
  question: null,
  difficulty: 2,
  qShownAt: 0,
  answering: false,
  tools: { canPronounce: false, insightId: null },
};
let assessment = { total: 16, idx: 0, answering: false, qShownAt: 0 };

// ═══════════ 初始化 ═══════════
async function init() {
  try {
    user = await api("/api/profile");
  } catch (e) {
    user = null;
  }
  // 应用持久化主题(?theme= 供调试直达)
  const params = new URLSearchParams(location.search);
  const tq = params.get("theme");
  if (user) applyTheme(tq === "western" || tq === "east" ? tq : (user.theme || "western"));
  else if (tq === "east") applyTheme("east");
  preloadSfx(); // 后台预加载真实音效 mp3(播放时才需要用户手势,解码不依赖)
  if (user && user.onboarded) {
    enterGame();
  } else {
    showView("welcome");
    // 欢迎页主角徽章跟随主题角色(西幻冒险者 / 东方侠客)
    const emblem = document.querySelector(".hero-emblem img");
    if (emblem) emblem.src = PLAYER_IMG("idle");
  }
  // 开发/测试便利:?view=vault 直达某个视图(不改变正常流程)
  const dv = params.get("view");
  if (dv === "battle") {
    if (!mapData) mapData = await api("/api/map");
    startBattle(0, parseInt(params.get("difficulty") || "1", 10));
  } else if (dv === "word" && params.get("id")) openWord(params.get("id"));
  else if (dv === "practice" && params.get("mode")) startPractice(params.get("mode"));
  else if (dv && $("view-" + dv)) goNav(dv);
}

async function refreshProfile() {
  user = await api("/api/profile");
  renderHud(user);
  return user;
}

// ═══════════ 进入游戏主界面 ═══════════
async function enterGame() {
  await refreshProfile();
  $("hud").classList.remove("hidden");
  $("nav").classList.remove("hidden");
  renderMap();
  goNav("map");
}

// ═══════════ 底部导航 ═══════════
document.querySelectorAll(".nav-btn").forEach((btn) => {
  btn.addEventListener("click", () => goNav(btn.dataset.nav));
});

function setNavActive(name) {
  document.querySelectorAll(".nav-btn").forEach((b) =>
    b.classList.toggle("active", b.dataset.nav === name)
  );
}

function goNav(name) {
  setNavActive(name);
  if (name === "map") renderMap();
  else if (name === "tasks") renderTasks();
  else if (name === "vault") renderVault();
  else if (name === "shop") renderShop();
  else if (name === "inventory") renderInventory();
  else if (name === "growth") renderGrowth();
  else if (name === "settings") renderSettings();
  showView(name);
}

// ═══════════ 主题引擎:切换动画 + 全站联动 ═══════════
let themeTransitionTimer = null;
function themeTransition() {
  const ov = $("theme-transition");
  if (!ov) return;
  ov.classList.remove("hidden");
  ov.classList.remove("show");
  void ov.offsetWidth; // 重触发动画
  ov.classList.add("show");
  clearTimeout(themeTransitionTimer);
  themeTransitionTimer = setTimeout(() => {
    ov.classList.add("hidden");
    ov.classList.remove("show");
  }, 900);
}

// 应用主题:切换 CSS 变量集 + 重绘当前界面
function applyTheme(t) {
  theme = t;
  document.documentElement.dataset.theme = t; // CSS 仅匹配 [data-theme="east"]
  // 设置页主题卡片高亮(如已打开)
  document.querySelectorAll("#theme-pick .tp").forEach((el) =>
    el.classList.toggle("active", el.dataset.theme === t)
  );
  // HUD 头像 + 设置页头像跟随主题角色
  if (user) {
    const hudImg = $("hud-avatar") ? $("hud-avatar").querySelector("img") : null;
    if (hudImg) hudImg.src = PLAYER_IMG("idle");
    const stImg = document.querySelector("#set-avatar img");
    if (stImg) stImg.src = PLAYER_IMG("idle");
  }
  // 已进入游戏 → 重绘当前视图让材质/场景/怪物全部换装
  if ($("hud") && !$("hud").classList.contains("hidden")) {
    const active = document.querySelector(".view.active");
    if (active) {
      const id = active.id.replace("view-", "");
      if (id === "map") renderMap();
      else if (id === "battle") renderBattleTheme();
    }
  }
}

// 战斗中换装:场景与玩家换主题,怪物沿用其名对应图(怪名即主题)
function renderBattleTheme() {
  if (!battle.active) return;
  const b = $("battle-scene");
  if (b && battle.regionIdx != null) b.style.backgroundImage = `url("${sceneImg(battle.regionIdx)}")`;
  setPlayerState("idle");
}

// 用户触发切换:过场动画 + 本地应用 + 持久化
async function setTheme(t) {
  if (t === theme) return;
  themeTransition();
  applyTheme(t);
  try {
    await api("/api/settings", { method: "PATCH", body: JSON.stringify({ theme: t }) });
    if (user) user.theme = t;
    toast(`已切换到${t === "east" ? "东方山海" : "西幻王国"}世界`);
  } catch (e) {
    toast(e.message);
  }
}

// ═══════════ 设置页 ═══════════
function setSeg(sel, v) {
  document.querySelectorAll(sel + " .seg-btn").forEach((b) =>
    b.classList.toggle("active", parseInt(b.dataset.v, 10) === v)
  );
}

async function renderSettings() {
  await refreshProfile();
  const img = document.querySelector("#set-avatar img");
  if (img) img.src = PLAYER_IMG("idle");
  $("set-nick").textContent = user.nickname || "冒险者";
  $("set-username").textContent = user.username ? `@${user.username}` : "";
  $("set-meta").textContent = `Lv.${user.level} · 目标 ${user.target_score} 分 · 每日 ${user.daily_minutes} 分钟`;
  $("inp-nick2").value = user.nickname || "";
  document.querySelectorAll("#theme-pick .tp").forEach((el) =>
    el.classList.toggle("active", el.dataset.theme === theme));
  setSeg("#seg-score2", user.target_score);
  setSeg("#seg-min2", user.daily_minutes);
  const logged = !!user.username;
  $("set-acct").textContent = logged ? `已登录 @${user.username}` : "本地存档(未登录)";
  $("btn-logout").classList.toggle("hidden", !logged);
}

// 设置入口在底部导航(data-nav="settings"),齿轮已从 HUD 移除(误触 TapTap 浮层)

// 主题卡片
$("theme-pick").addEventListener("click", (e) => {
  const t = e.target.closest(".tp");
  if (!t) return;
  setTheme(t.dataset.theme);
});

// 保存名号
$("btn-save-nick").addEventListener("click", async () => {
  const nick = $("inp-nick2").value.trim();
  if (!nick) { toast("名号不能为空"); return; }
  try {
    await api("/api/settings", { method: "PATCH", body: JSON.stringify({ nickname: nick }) });
    toast("名号已保存");
  } catch (e) {
    toast(e.message);
  }
  renderSettings();
});

// 目标分数 / 每日投入
$("seg-score2").addEventListener("click", async (e) => {
  const b = e.target.closest(".seg-btn");
  if (!b) return;
  try {
    await api("/api/settings", { method: "PATCH", body: JSON.stringify({ target_score: parseInt(b.dataset.v, 10) }) });
    toast("目标分数已更新");
  } catch (err) { toast(err.message); }
  renderSettings();
});

$("seg-min2").addEventListener("click", async (e) => {
  const b = e.target.closest(".seg-btn");
  if (!b) return;
  try {
    await api("/api/settings", { method: "PATCH", body: JSON.stringify({ daily_minutes: parseInt(b.dataset.v, 10) }) });
    toast("每日投入已更新");
  } catch (err) { toast(err.message); }
  renderSettings();
});

// 退出登录
$("btn-logout").addEventListener("click", async () => {
  await api("/api/auth/logout", { method: "POST" }).catch(() => {});
  toast("已退出登录");
  user = null;
  $("hud").classList.add("hidden");
  $("nav").classList.add("hidden");
  showView("welcome");
});

// ═══════════ 登录/注册 ═══════════
let authMode = "login"; // login | register
function openAuth(mode) {
  authMode = mode;
  $("auth-title").textContent = mode === "login" ? "登录" : "注册";
  $("auth-submit").textContent = mode === "login" ? "登录" : "创建账号";
  $("auth-toggle-text").textContent = mode === "login" ? "还没有账号?" : "已有账号?";
  $("auth-switch").textContent = mode === "login" ? "去注册" : "去登录";
  $("auth-username").value = "";
  $("auth-password").value = "";
  $("auth-error").textContent = "";
  $("auth-error").classList.add("hidden");
  $("modal-auth").classList.remove("hidden");
  setTimeout(() => $("auth-username").focus(), 80);
}
function closeAuth() { $("modal-auth").classList.add("hidden"); }

$("auth-cancel").addEventListener("click", closeAuth);
$("auth-switch").addEventListener("click", () =>
  openAuth(authMode === "login" ? "register" : "login")
);

async function submitAuth() {
  const username = $("auth-username").value.trim();
  const password = $("auth-password").value;
  if (!username || !password) {
    $("auth-error").textContent = "请填写用户名和密码";
    $("auth-error").classList.remove("hidden");
    return;
  }
  const btn = $("auth-submit");
  btn.disabled = true;
  $("auth-error").classList.add("hidden");
  try {
    await api(authMode === "login" ? "/api/auth/login" : "/api/auth/register", {
      method: "POST",
      body: JSON.stringify({ username, password }),
    });
    closeAuth();
    toast(authMode === "login" ? "登录成功" : "注册成功,欢迎加入远征!");
    await refreshProfile();
    if (user && user.onboarded) enterGame();
    else {
      if (user) $("inp-nick").value = user.nickname;
      showView("onboarding");
    }
  } catch (e) {
    $("auth-error").textContent = e.message;
    $("auth-error").classList.remove("hidden");
  } finally {
    btn.disabled = false;
  }
}
$("auth-submit").addEventListener("click", submitAuth);
$("auth-password").addEventListener("keydown", (e) => { if (e.key === "Enter") submitAuth(); });

// ═══════════ 欢迎页 ═══════════
$("btn-start").addEventListener("click", () => {
  // 已开垦过冒险的账号直接进图,新玩家才走测评
  if (user && user.onboarded) { enterGame(); return; }
  if (user) $("inp-nick").value = user.nickname;
  showView("onboarding");
});

// ═══════════ 目标设置 ═══════════
let targetScore = 425, dailyMin = 20;
$("seg-score").addEventListener("click", (e) => {
  const b = e.target.closest(".seg-btn");
  if (!b) return;
  document.querySelectorAll("#seg-score .seg-btn").forEach((x) => x.classList.remove("active"));
  b.classList.add("active");
  targetScore = parseInt(b.dataset.v, 10);
});
$("seg-min").addEventListener("click", (e) => {
  const b = e.target.closest(".seg-btn");
  if (!b) return;
  document.querySelectorAll("#seg-min .seg-btn").forEach((x) => x.classList.remove("active"));
  b.classList.add("active");
  dailyMin = parseInt(b.dataset.v, 10);
});

$("btn-onboard").addEventListener("click", async () => {
  const nick = $("inp-nick").value.trim() || "冒险者";
  await api("/api/onboarding", {
    method: "POST",
    body: JSON.stringify({ nickname: nick, target_score: targetScore, daily_minutes: dailyMin }),
  });
  startAssessment();
});

// ═══════════ 测评 ═══════════
async function startAssessment() {
  showView("assessment");
  assessment.answering = true;
  $("assess-loading").classList.remove("hidden");
  $("assess-q").classList.add("hidden");
  const r = await api("/api/assessment/start", { method: "POST" });
  assessment.total = r.total;
  assessment.idx = 0;
  renderAssessProgress();
  const q = await api("/api/assessment/question");
  assessment.answering = false;
  renderAssessQuestion(q);
}

function renderAssessProgress() {
  $("assess-progress").textContent = `${assessment.idx + 1} / ${assessment.total}`;
  $("assess-fill").style.width = (assessment.idx / assessment.total * 100) + "%";
}

function renderAssessQuestion(q) {
  $("assess-loading").classList.add("hidden");
  $("assess-q").classList.remove("hidden");
  renderAssessProgress();
  $("assess-word").textContent = titleCase(q.word);
  const box = $("assess-options");
  box.innerHTML = "";
  q.options.forEach((opt) => {
    const b = document.createElement("button");
    b.className = "quiz-opt";
    b.textContent = opt;
    b.addEventListener("click", () => assessPick(b, q));
    box.appendChild(b);
  });
  assessment.qShownAt = Date.now();
}

async function assessPick(btn, q) {
  if (assessment.answering) return;
  assessment.answering = true;
  const picked = btn.textContent;
  const correct = picked === q.answer;
  const rt = (Date.now() - assessment.qShownAt) / 1000;

  document.querySelectorAll("#assess-options .quiz-opt").forEach((o) => {
    o.disabled = true;
    if (o.textContent === q.answer) o.classList.add("correct");
  });
  if (!correct) btn.classList.add("wrong");

  await api("/api/assessment/answer", {
    method: "POST",
    body: JSON.stringify({ idx: q.idx, correct, response_time: rt }),
  });

  setTimeout(() => {
    assessment.idx = q.idx + 1;
    assessment.answering = false;
    if (assessment.idx >= assessment.total) {
      loadAssessmentResult();
    } else {
      api("/api/assessment/question").then(renderAssessQuestion);
    }
  }, 550);
}

async function loadAssessmentResult() {
  $("assess-loading").classList.remove("hidden");
  $("assess-q").classList.add("hidden");
  const res = await api("/api/assessment/result");
  $("res-level").textContent = "Lv." + res.level;
  $("res-score").textContent = `${res.correct_count}/${res.total}`;
  $("res-open").textContent = `${res.unlock_region + 1} 关`;
  $("res-unlock").textContent = "提前开放:" + res.unlock_regions.join(" · ");
  $("res-tip").textContent = res.correct_count >= res.total
    ? `全对!从 Lv.${res.level} 开启全部区域远征。`
    : `答对 ${res.correct_count}/${res.total} 题,判定 Lv.${res.level}。全对可到 Lv.9 并开启全部区域,继续刷词解锁更多区域!`;
  showView("result");
  await refreshProfile();
}

$("btn-to-map").addEventListener("click", enterGame);

// ═══════════ 地图 ═══════════
const REGION_DIFF = [1, 2, 2, 3, 4, 5]; // 区域 → 单词难度(6=Boss)

async function renderMap() {
  mapData = await api("/api/map");
  $("map-player").textContent = `Lv.${user.level}`;
  const box = $("map-regions");
  box.innerHTML = "";
  mapData.regions.forEach((r, i) => {
    const div = document.createElement("div");
    // 每关尽头都有章节 Boss;末关(龙巢)保留红框终极强调
    const isFinal = i === mapData.regions.length - 1;
    div.className = "map-region" + (r.unlocked ? "" : " locked") + (r.boss && isFinal ? " boss-region" : "");
    if (r.unlocked) {
      // 已解锁:展示怪物样貌 + 击杀进度(达标解锁下一关)
      const badge = `<span class="r-badge"><img src="${monsterImg(r.monster)}" alt="">BOSS</span>`;
      const prog = r.kill_target != null
        ? `<span class="r-prog">已灭 <b>${r.kills}</b>/${r.kill_target} · 解锁下一关</span>`
        : "";
      div.innerHTML = `
        <span class="r-icon"><img src="${monsterImg(r.monster)}" alt=""></span>
        <span>
          <span class="r-name">${r.name}</span><br>
          <span class="r-monster">怪物:${r.monster} · 难度 ${REGION_DIFF[i]}</span>${prog}
        </span>
        ${badge}`;
      div.addEventListener("click", () => startBattle(i, REGION_DIFF[i]));
    } else {
      // 未解锁:只给名字 + 锁 + 通关条件,不泄露怪物/场景样貌(保持新鲜感)
      const prev = mapData.regions[i - 1];
      div.innerHTML = `
        <span class="r-icon"><img src="${ASSET}/icons/icon_lock.svg" alt="未解锁"></span>
        <span>
          <span class="r-name">${r.name}</span><br>
          <span class="r-monster">怪物:？？？</span>
          <span class="r-lock">通关「${prev.name}」后解锁 · 进度 ${prev.kills}/${prev.kill_target}</span>
        </span>`;
    }
    box.appendChild(div);
  });
  // 每日任务摘要
  const t = await api("/api/tasks");
  const p = t.progress, g = t.goals;
  $("map-dailies").innerHTML =
    `<img class="mini-ico" src="${ASSET}/icons/icon_quest.svg" alt="">今日契约:击败 <b>${p.monsters_killed}/${g.monsters_killed}</b> 怪 · ` +
    `学习 <b>${p.words_learned}/${g.words_learned}</b> 词 · ` +
    `连击 <b>${p.max_combo}/${g.max_combo}</b>`;
}

// ═══════════ 战斗 ═══════════
async function startBattle(regionIdx, difficulty) {
  const region = mapData.regions[regionIdx];
  const r = await api("/api/battle/start", { method: "POST", body: JSON.stringify({ difficulty, region_idx: regionIdx }) });
  battle = {
    active: true,
    monster: r.monster,
    question: r.question,
    difficulty,
    answering: false,
    tools: { canPronounce: false, insightId: null },
    regionName: region.name,
    regionIdx,
    isBoss: !!region.boss,
    // Boss 章节剧情:每关都有专属 Boss,击杀小兵计数到节点触发登场/对话/最终战
    bossEncounter: !!BOSSES[regionIdx],
    bossKey: BOSSES[regionIdx] ? BOSSES[regionIdx].key : "",
    minionKills: 0,
    bossFight: false,
    // BOSSES 每项键是 west/east(theme 全局变量值是 'western'/'east',需换算)
    bossZh: BOSSES[regionIdx] ? BOSSES[regionIdx][theme === "east" ? "east" : "west"].zh : "",
    bossEn: BOSSES[regionIdx] ? BOSSES[regionIdx][theme === "east" ? "east" : "west"].en : "",
  };
  // 读取背包判断可用道具
  try {
    const inv = await api("/api/inventory");
    battle.tools.canPronounce = inv.items.some((i) => i.name === "echo_earring" && i.equipped);
    const eye = inv.items.find((i) => i.name === "insight_eye");
    battle.tools.insightId = eye && eye.quantity > 0 ? eye.id : null;
  } catch (e) {}

  $("battle-region").textContent = region.name;
  $("battle-scene").style.backgroundImage = `url("${sceneImg(regionIdx)}")`;
  // Boss 压迫红框只在末关;每关尽头都有章节 Boss(遭遇流程走 bossEncounter)
  $("battle-box").classList.toggle("boss", regionIdx === mapData.regions.length - 1);
  $("battle-monster").innerHTML = `<img src="${monsterImg(r.monster.name)}" alt="${r.monster.name}">`;
  setPlayerState("idle");
  renderMonsterHp(r.monster.max_hp, r.monster.max_hp);
  $("battle-player-max").textContent = r.max_hp;
  $("battle-player-hp").textContent = r.player_hp;
  $("player-hp").style.width = (r.player_hp / r.max_hp * 100) + "%";
  setCombo(0);
  renderBattleInfo();
  showView("battle");
  renderBattleQuestion(r.question);
  if (battle.bossEncounter) {
    toast(`此关有 Boss 出没:${battle.bossZh}!击败 ${BOSS_MINION_TOTAL} 只小兵后,王者将现身。`, 3200);
  }
}

function setCombo(c) {
  const el = $("battle-combo");
  el.dataset.c = c;
  el.innerHTML = c > 0
    ? `<img src="${ASSET}/icons/icon_fire.svg" alt=""> Combo <b>×${c}</b>`
    : `Combo <b>×0</b>`;
  if (c > 0) {
    el.classList.remove("pop");
    void el.offsetWidth; // 重触发动画
    el.classList.add("pop");
  }
}

function renderMonsterHp(hp, max) {
  $("monster-hp").style.width = Math.max(0, hp / max * 100) + "%";
  $("monster-hp-text").textContent = `${Math.max(0, hp)}/${max}`;
}

function renderPlayerHp(hp, max) {
  $("battle-player-hp").textContent = Math.max(0, hp);
  $("player-hp").style.width = Math.max(0, hp / max * 100) + "%";
}

function floatText(text, cls, aboveMonster) {
  const wrap = $("float-damage");
  const span = document.createElement("div");
  span.className = "float-num " + cls;
  span.textContent = text;
  wrap.appendChild(span);
  span.style.top = aboveMonster ? "10px" : "150px";
  setTimeout(() => span.remove(), 1100);
}

// 战斗信息条:昵称/等级/金币/修为/怪物名(全部来自真实 profile 数据)
function renderBattleInfo() {
  if (!user) return;
  $("battle-player-name").textContent = user.nickname || "冒险者";
  $("battle-player-level").textContent = "Lv." + user.level;
  $("battle-player-avatar").innerHTML = `<img src="${PLAYER_IMG("idle")}" alt="${user.nickname || "冒险者"}">`;
  $("battle-gold").textContent = user.gold;
  $("battle-player-atk").textContent = user.attack || 10;
  $("battle-xp-fill").style.width = (user.xp_percent || 0) + "%";
  $("battle-xp-text").textContent = `${user.xp_in_level || 0}/${user.xp_to_next || 0}`;
  if (battle.monster) $("monster-name").textContent = battle.monster.name;
}

function renderBattleQuestion(q) {
  battle.question = q;
  battle.answering = false;
  battle.qShownAt = Date.now();
  $("battle-word").textContent = titleCase(q.word);
  const ph = $("battle-phonetic");
  if (q.phonetic) { ph.textContent = q.phonetic; ph.classList.remove("hidden"); }
  else ph.classList.add("hidden");
  const box = $("battle-options");
  box.innerHTML = "";
  q.options.forEach((opt) => {
    const b = document.createElement("button");
    b.className = "quiz-opt";
    b.textContent = opt;
    b.addEventListener("click", () => battlePick(b));
    box.appendChild(b);
  });
  // 道具按钮
  $("btn-pronounce").classList.toggle("hidden", !battle.tools.canPronounce);
  $("btn-insight").classList.toggle("hidden", !battle.tools.insightId);
}

async function battlePick(btn) {
  if (battle.answering) return;
  battle.answering = true;
  const picked = btn.textContent;
  const correct = picked === battle.question.correct;
  const rt = (Date.now() - battle.qShownAt) / 1000;

  document.querySelectorAll("#battle-options .quiz-opt").forEach((o) => {
    o.disabled = true;
    if (o.textContent === battle.question.correct) o.classList.add("correct");
  });
  if (!correct) btn.classList.add("wrong");

  let res;
  try {
    res = await api("/api/battle/answer", {
      method: "POST",
      body: JSON.stringify({
        correct,
        word_id: battle.question.word_id,
        response_time: rt,
        difficulty: battle.difficulty,
      }),
    });
  } catch (e) {
    toast(e.message);
    battle.answering = false;
    return;
  }

  // 反馈 + 数值更新
  if (correct) {
    const m = $("battle-monster");
    m.classList.remove("hit", "flash");
    // 角色朝上挥剑 + 剑气从角色射向怪物;命中时才受击/弧光/音效/伤害浮字
    setPlayerState("attack");
    setTimeout(() => setPlayerState("idle"), 440); // 与 playerSwing .4s 动画对齐,挥剑播完再复原
    playBeam(() => {
      void m.offsetWidth;
      m.classList.add("hit", "flash");
      playSlash();
      // 打击音效:按怪物身份发声(史莱姆=果冻啵 / 狼人=狼嚎 / 龙=龙吼……);暴击更深
      if (res.damage >= 30) sfxCrit(battle.monster.name);
      else sfxHit(battle.monster.name);
      floatText("-" + res.damage, res.damage >= 30 ? "crit" : "dmg", true);
    });
    setCombo(res.combo);
    renderMonsterHp(res.monster_hp, res.monster_max_hp);
    renderPlayerHp(res.player_hp, $("battle-player-max").textContent);
    if (res.gold_gained > 0) {
      burstSparkle();
      toast(`击败了 ${battle.monster.name}!金币 +${res.gold_gained} · XP +${res.xp_gained}`, 1800);
    }
  } else {
    shakeScreen();
    setMonsterState("attack", 520);
    setPlayerState(res.shield_used ? "idle" : "hit");
    if (res.shield_used) {
      toast("守护之盾替你挡下了这次伤害!", 2000);
      sfxShield();
      renderPlayerHp(res.player_hp, $("battle-player-max").textContent);
    } else {
      floatText("-" + res.player_hurt, "hurt", false);
      sfxPlayerHurt();
      renderPlayerHp(res.player_hp, $("battle-player-max").textContent);
      setTimeout(() => setPlayerState("idle"), 520);
    }
    setCombo(0);
  }

  // 升级提示
  if (res.new_level > (user ? user.level : 1)) {
    toast(`升级!到达 Lv.${res.new_level}`, 2600);
  }
  await refreshProfile();
  renderBattleInfo();

  // 结算分支
  if (res.player_defeated) {
    setPlayerState("defeat");
    sfxPlayerDefeat();
    setTimeout(() => {
      $("defeat-msg").textContent = `${battle.monster.name} 夺走了你的生命。回到营地休整,再战一场。`;
      $("modal-defeat").classList.remove("hidden");
      battle.active = false;
    }, 900);
    return;
  }
  if (res.boss_defeated) {
    // Boss 最终战胜利:王者倒下 → 胜利结算
    setMonsterState("defeat");
    sfxMonsterKill(battle.monster.name);
    setTimeout(() => showBossVictory(), 1000);
    return;
  }
  if (res.monster_defeated) {
    setMonsterState("defeat");
    sfxMonsterKill(battle.monster.name);
    setTimeout(() => {
      const bossStep = bossFlowOnKill();
      if (bossStep) {
        showBossStep(bossStep, res); // 小兵打够了,触发 Boss 剧情(登场/对话/最终战)
        return;
      }
      resumeMinionBattle(res); // 普通小兵:换后端已生成的下一只怪
    }, 1000);
    return;
  }
  // 没结束 → 出下一题
  setMonsterState("idle");
  setPlayerState("idle");
  setTimeout(() => {
    api("/api/battle/question", {})
      .then((q) => renderBattleQuestion(q))
      .catch(() => battle.answering = false);
  }, 450);
}

// ═══════════ Boss 章节剧情(仅第1关新手村触发) ═══════════
// 流程:小兵×3 → [登场] → 对话 → 小兵×3 → 对话 → 小兵×4 → 最终战 → 胜利
// 英语台词用浏览器原生 speechSynthesis 朗读(无外部 TTS),玩家从英语选项回应。
// 每个区域都有一只专属 Boss(西幻/东方双形象),身份、声音、台词、怒吼各不相同。
const BOSSES = [
  { key: "goblin_king",      west: { zh: "哥布林王",     en: "Goblin King" },             east: { zh: "草木妖王", en: "Grass King" } },
  { key: "goblin_war_chief", west: { zh: "哥布林大酋长", en: "Goblin War Chief" },        east: { zh: "青竹狐王", en: "Fox King" } },
  { key: "goblin_high_king", west: { zh: "哥布林国王",   en: "Goblin High King" },        east: { zh: "狐妖大君", en: "Fox Sovereign" } },
  { key: "wolf_king",        west: { zh: "狼王",         en: "Werewolf Alpha" },          east: { zh: "苍狼王",   en: "Grey Wolf King" } },
  { key: "demon_lord",       west: { zh: "深渊魔王",     en: "Demon Lord" },              east: { zh: "炎魔大君", en: "Flame Fiend" } },
  { key: "dragon_emperor",   west: { zh: "四级龙王",     en: "Dragon Emperor" },          east: { zh: "深渊龙皇", en: "Abyss Dragon Emperor" } },
];
const BOSS_MINION_TOTAL = 10; // 最终战前需击败的小兵总数
const BOSS_SCRIPTS = [
  // 区域0 · 哥布林王(村庄恶霸)
  {
    intro: { en: "Who dares to step into my village?", zh: "谁胆敢闯入我的村庄?" },
    dial1: {
      en: "You can fight well... But can you beat ME?", zh: "你打架有两下子……可你能打败本大王吗?",
      choices: [
        { text: "I will defeat you!", good: true, reactEn: "Hmph! Bold words, little one!", reactZh: "哼!口气不小,小家伙!" },
        { text: "I just want to practice English.", good: false, reactEn: "Ha! Then be my training dummy!", reactZh: "哈!那就当本大王的陪练吧!" },
      ],
    },
    dial2: {
      en: "Impressive! But my true power is just beginning!", zh: "有点本事!但本大王的真本事才刚刚开始!",
      choices: [
        { text: "I am not afraid of you!", good: true, reactEn: "Hah! You will be!", reactZh: "哈!你迟早会怕的!" },
        { text: "Please be gentle with me...", good: false, reactEn: "Begging already? What a bore!", reactZh: "这就求饶了?真没意思!" },
      ],
    },
    final: { en: "Enough games! Face me at full power!", zh: "玩够了!拿出全力来面对本大王吧!" },
    victory: { en: "You... you won. Go on, little hero.", zh: "你……你赢了。走吧,小英雄。" },
  },
  // 区域1 · 哥布林大酋长(好战狂酋)
  {
    intro: { en: "More warriors? Smash them all, my boys!", zh: "又有勇士送上门?小的们,给我砸扁他们!" },
    dial1: {
      en: "You are tougher than the last bunch. Good - more fun for ME!", zh: "你比上一拨硬气。好——本酋长越打越来劲!",
      choices: [
        { text: "My axe is hungry. Feed it!", good: true, reactEn: "Ha! Then be my sharpening stone!", reactZh: "哈!那就当本酋长的磨刀石吧!" },
        { text: "I came to practice English.", good: false, reactEn: "A talking book? I will smash it too!", reactZh: "会说话的书?照样砸扁!" },
      ],
    },
    dial2: {
      en: "Keep coming! I can fight all day!", zh: "继续来!本酋长能打一整天!",
      choices: [
        { text: "I will cut off your feathers!", good: true, reactEn: "Bold words for a snack!", reactZh: "小点心也敢口出狂言!" },
        { text: "Maybe we can talk this over?", good: false, reactEn: "Talk? War never talks!", reactZh: "谈?打仗哪有谈判的!" },
      ],
    },
    final: { en: "ENOUGH! Face the war chief of this forest!", zh: "够了!来面对这片森林的战酋吧!" },
    victory: { en: "Ugh... a worthy warrior. The forest is yours... for now.", zh: "呃……好样的勇士。这片森林归你了……暂时是。" },
  },
  // 区域2 · 哥布林国王(黄金暴君)
  {
    intro: { en: "Kneel, or be crushed beneath my golden crown.", zh: "跪下,否则就压死在我的金冠之下。" },
    dial1: {
      en: "You have spirit, adventurer. A pity it will break.", zh: "你有骨气,冒险者。可惜它就要折断了。",
      choices: [
        { text: "My sword does not kneel!", good: true, reactEn: "Then it shall break WITH you!", reactZh: "那就连人带剑一起断!" },
        { text: "I am lost. Can you help me?", good: false, reactEn: "Help? My dungeon says otherwise!", reactZh: "帮忙?我的地牢可不这么想!" },
      ],
    },
    dial2: {
      en: "My castle has never fallen. It will not fall to you.", zh: "我的城堡从未陷落,也不会败在你手里。",
      choices: [
        { text: "I will raise my banner here!", good: true, reactEn: "Ha! A banner of defeat!", reactZh: "哈!一面战败的旗帜!" },
        { text: "This is just a grammar test!", good: false, reactEn: "Grammar will not save you now!", reactZh: "语法现在可救不了你!" },
      ],
    },
    final: { en: "I shall end this farce myself. Taste royal steel!", zh: "我要亲自结束这场闹剧。尝尝王者的利刃吧!" },
    victory: { en: "The crown... is yours now. Guard it well, young king.", zh: "这顶王冠……归你了。好好守着它吧,年轻的王。" },
  },
  // 区域3 · 狼王(荒原狼王)
  {
    intro: { en: "GRRR... fresh meat in MY hunting grounds.", zh: "吼……竟敢闯进本王的地盘,好一块鲜肉。" },
    dial1: {
      en: "Your bones will howl in my den, hunter.", zh: "你的骨头会在我的巢穴里嚎叫,猎人。",
      choices: [
        { text: "I am the hunter here!", good: true, reactEn: "Tonight, the prey hunts!", reactZh: "今晚,猎物要反杀!" },
        { text: "I smell like a friend?", good: false, reactEn: "Friend? I smell dinner!", reactZh: "朋友?我闻到的明明是晚餐!" },
      ],
    },
    dial2: {
      en: "The pack howls for your blood. I will not hold them back.", zh: "狼群正为你的血嚎叫。本王不会再拦着它们。",
      choices: [
        { text: "Call them off!", good: true, reactEn: "Too late - the hunt is on!", reactZh: "太迟了——猎杀已经开始!" },
        { text: "I brought meat as a gift!", good: false, reactEn: "HA! You ARE the meat!", reactZh: "哈!你本身就是那块肉!" },
      ],
    },
    final: { en: "TIME TO HUNT, PREY. FACE THE ALPHA!", zh: "猎物,该狩猎了。面对狼王吧!" },
    victory: { en: "You... survived the hunt. Run. Before the moon turns.", zh: "你……活过了这场猎杀。跑吧,趁月圆之前。" },
  },
  // 区域4 · 深渊魔王(深渊之主)
  {
    intro: { en: "A mortal, in my abyss? How amusing.", zh: "一个凡人,闯进我的深渊?真有意思。" },
    dial1: {
      en: "Your little sword cannot scratch my shadow.", zh: "你那把小剑,连我的影子都划不破。",
      choices: [
        { text: "I fear no shadow!", good: true, reactEn: "Then face the dark itself!", reactZh: "那就去直面黑暗本身吧!" },
        { text: "Is this a vocabulary test?", good: false, reactEn: "Your last lesson ends here!", reactZh: "你的最后一课到此为止!" },
      ],
    },
    dial2: {
      en: "I have devoured heroes greater than you.", zh: "比你还了不起的英雄,我吞噬过不知多少。",
      choices: [
        { text: "Then I shall be your undoing!", good: true, reactEn: "The abyss hungers for your fear!", reactZh: "深渊正渴望你的恐惧!" },
        { text: "Please let me pass, Master Demon.", good: false, reactEn: "Groveling suits you, mortal.", reactZh: "卑躬屈膝很适合你,凡人。" },
      ],
    },
    final: { en: "Behold! My true form! Bow to the darkness!", zh: "看好了!我的真身!向黑暗臣服吧!" },
    victory: { en: "Impossible... the abyss... recedes... begone, light.", zh: "不可能……深渊……在退却……滚开,光明。" },
  },
  // 区域5 · 四级龙王(千年龙皇)
  {
    intro: { en: "A thousand years, and still they send ants to my nest.", zh: "千年了,他们还是派蚂蚁来我的巢穴。" },
    dial1: {
      en: "I have hoarded the ages. What do YOU hoard, little one?", zh: "我囤积了千年的时光。小家伙,你又囤了什么?",
      choices: [
        { text: "Courage, and a sharp sword!", good: true, reactEn: "Courage burns well, little flame!", reactZh: "勇气很好烧,小火焰!" },
        { text: "I just want one dragon word!", good: false, reactEn: "One word? I shall give you a ROAR!", reactZh: "一个词?那本王就赏你一声龙吼!" },
      ],
    },
    dial2: {
      en: "Your courage is a small flame. My breath is the sun.", zh: "你的勇气不过一点小火苗,而本王吐息如骄阳。",
      choices: [
        { text: "Small flames can become infernos!", good: true, reactEn: "Then learn the meaning of FIRE!", reactZh: "那就来领悟什么叫烈火燎原!" },
        { text: "Teach me a long word, Great Dragon!", good: false, reactEn: "So be it - DEFEAT is your lesson!", reactZh: "好——'失败'就是本王教你的词!" },
      ],
    },
    final: { en: "ENOUGH! Bask in the fire of the Dragon Emperor!", zh: "够了!沐浴在龙皇的烈焰之下吧!" },
    victory: { en: "Hm... the first to pierce my scales in centuries. Fly high, champion.", zh: "嗯……数百年来第一个刺穿我龙鳞的勇士。飞得更高吧,冠军。" },
  },
];

let bossNextAction = null; // [继续] 按钮绑定的下一步

// 小兵被击杀后推进剧情:返回要演的剧情名,无则继续正常刷兵
function bossFlowOnKill() {
  if (!battle.bossEncounter || battle.bossFight) return null;
  battle.minionKills++;
  if (battle.minionKills === 3) return "intro";   // 第3只 → 登场
  if (battle.minionKills === 6) return "dial2";   // 第6只 → 第二段对话
  if (battle.minionKills === BOSS_MINION_TOTAL) return "final"; // 第10只 → 最终战宣言
  return null;
}

// 打开剧情遮罩(头部:当前主题的 Boss 名 + 形象)
function openBossOverlay() {
  $("boss-overlay").classList.remove("hidden");
  $("boss-avatar").src = monsterImg(battle.bossZh, "idle");
  $("boss-card-zh").textContent = battle.bossZh;
  $("boss-card-en").textContent = battle.bossEn;
  const card = $("boss-overlay").querySelector(".modal-box");
  card.classList.remove("pop");
  void card.offsetWidth;
  card.classList.add("pop");
}
function closeBossOverlay() { $("boss-overlay").classList.add("hidden"); }

// 显示台词(英文朗读 + 中文字幕);用当前 Boss 的声音档案朗读
function showBossLine(en, zh) {
  $("boss-line-en").textContent = en;
  $("boss-line-zh").textContent = zh;
  speakBossLine(en, battle.bossKey);
}

// 演出剧情主流程(按区域取对应 Boss 的台词)
function showBossStep(step, res) {
  openBossOverlay();
  const s = (BOSS_SCRIPTS[battle.regionIdx] || BOSS_SCRIPTS[0])[step];
  $("boss-choices").classList.add("hidden");
  $("boss-choices").innerHTML = "";
  $("btn-boss-repeat").classList.remove("hidden");
  $("btn-boss-next").classList.add("hidden");
  showBossLine(s.en, s.zh);
  bossNextAction = null;
  if (step === "intro") {
    sfxBossRoar(battle.bossKey); // 登场怒吼:按 Boss 身份
    $("btn-boss-next").classList.remove("hidden");
    bossNextAction = () => showBossStep("dial1", res); // 念完 → 进入第一段对话
  } else if (step === "dial1" || step === "dial2") {
    renderBossChoices(s.choices, res);
  } else if (step === "final") {
    sfxBossRoar(battle.bossKey);
    $("btn-boss-next").classList.remove("hidden");
    bossNextAction = startBossFight; // 念完 → 开打
  }
}

// 渲染英语对话选项(玩家从英语选项里回应 Boss)
function renderBossChoices(choices, res) {
  const box = $("boss-choices");
  box.innerHTML = "";
  choices.forEach((c) => {
    const b = document.createElement("button");
    b.className = "boss-choice";
    b.textContent = c.text;
    b.addEventListener("click", () => {
      document.querySelectorAll(".boss-choice").forEach((x) => (x.disabled = true));
      // 回应后:Boss 按选对/选错给出不同反应,点[继续]回到小兵战斗
      showBossLine(c.reactEn, c.reactZh);
      bossNextAction = () => {
        closeBossOverlay();
        resumeMinionBattle(res);
      };
      $("btn-boss-next").classList.remove("hidden");
    });
    box.appendChild(b);
  });
  box.classList.remove("hidden");
}

// 剧情结束,回到小兵战斗(用击杀时后端已生成的下一个小兵)
function resumeMinionBattle(res) {
  if (res.next_monster) {
    battle.monster = res.next_monster;
    $("battle-monster").innerHTML = `<img src="${monsterImg(battle.monster.name)}" alt="${battle.monster.name}">`;
    $("monster-name").textContent = battle.monster.name;
    renderMonsterHp(res.next_monster.max_hp, res.next_monster.max_hp);
  }
  const m = $("battle-monster");
  m.classList.remove("hit", "flash");
  renderBattleQuestion(res.next_question);
  setMonsterState("idle");
}

// Boss 最终战:请求后端把小兵换成真身 Boss,血条变长、加压迫样式
async function startBossFight() {
  closeBossOverlay();
  const r = await api("/api/battle/boss", { method: "POST" }).catch(() => null);
  if (!r) { battle.answering = false; return; }
  battle.monster = r.monster;
  battle.bossFight = true;
  $("battle-box").classList.add("boss");
  $("battle-monster").innerHTML = `<img src="${monsterImg(battle.monster.name)}" alt="${battle.monster.name}">`;
  $("monster-name").textContent = battle.monster.name;
  renderMonsterHp(r.monster.max_hp, r.monster.max_hp);
  renderBattleQuestion(r.question);
  sfxBossRoar(battle.bossKey);
}

// Boss 被击杀:胜利结算
function showBossVictory() {
  $("victory-boss-name").textContent = battle.bossZh || battle.monster.name;
  const s = (BOSS_SCRIPTS[battle.regionIdx] || BOSS_SCRIPTS[0]).victory;
  $("victory-line").textContent = s.zh;
  speakBossLine(s.en, battle.bossKey);
  sfxVictory();
  $("modal-boss-victory").classList.remove("hidden");
  battle.active = false;
  battle.monster = null;
}

$("btn-boss-next").addEventListener("click", () => {
  const act = bossNextAction;
  bossNextAction = null;
  if (act) act();
});
$("btn-boss-repeat").addEventListener("click", () => {
  const en = $("boss-line-en").textContent || "";
  if (en) speakBossLine(en, battle.bossKey);
});
$("btn-victory-back").addEventListener("click", () => {
  $("modal-boss-victory").classList.add("hidden");
  renderMap();
  goNav("map");
  toast("讨伐成功!王者已伏诛,村落重归安宁", 2200);
});

// 发音(回响耳环):原生 speechSynthesis 优先,缺失时走离线 meSpeak 兜底
$("btn-pronounce").addEventListener("click", () => {
  const w = battle.question && battle.question.word;
  if (!w) return;
  speakWord(w);
});

// 洞察之眼:排除一个错误答案
$("btn-insight").addEventListener("click", async () => {
  if (!battle.tools.insightId) return;
  const q = battle.question;
  const wrongs = [...document.querySelectorAll("#battle-options .quiz-opt")].filter(
    (o) => o.textContent !== q.correct
  );
  if (!wrongs.length) return;
  const target = wrongs[Math.floor(Math.random() * wrongs.length)];
  target.classList.add("dimmed");
  await api("/api/inventory/use", {
    method: "POST",
    body: JSON.stringify({ item_id: battle.tools.insightId }),
  }).catch(() => {});
  battle.tools.insightId = null;
  $("btn-insight").classList.add("hidden");
  toast("洞察之眼:已排除一个错误答案", 1500);
});

$("btn-defeat-back").addEventListener("click", () => {
  $("modal-defeat").classList.add("hidden");
  renderMap();
  goNav("map");
});

// 战斗中途撤退:结束会话并返回营地(不必战败或靠底部导航切走)
async function retreatBattle() {
  try { await api("/api/battle/end", { method: "POST" }); } catch (e) {}
  battle.active = false;
  battle.monster = null;
  renderMap();
  goNav("map");
  toast("已撤退,返回营地休整", 1200);
}
$("btn-battle-retreat").addEventListener("click", retreatBattle);

// ═══════════ 词藏 (Vault) ═══════════
const vault = { status: "all", q: "", page: 1, size: 20 };
const VAULT_STATUS_LABEL = { unseen: "未学", weak: "薄弱", learning: "学习中", mastered: "已掌握" };
const PRACTICE_LABEL = { weak: "薄弱词挑战", review: "到期复习", unseen: "探索新词" };

async function renderVault() {
  const s = await api("/api/vault/stats");
  $("vault-stats").innerHTML = [
    ["未学", s.unseen, ""],
    ["薄弱", s.weak, s.weak > 0 ? "" : ""],
    ["学习中", s.learning, ""],
    ["已掌握", s.mastered, ""],
    ["到期复习", s.due_review, "due"],
    ["词库总数", s.total, ""],
  ]
    .map(([label, num, cls]) =>
      `<div class="vault-stat ${cls}"><span class="vs-num">${num}</span><span class="vs-label">${label}</span></div>`
    )
    .join("");
  await renderVaultList();
}

async function renderVaultList() {
  const r = await api(
    `/api/vault/words?status=${vault.status}&q=${encodeURIComponent(vault.q)}&page=${vault.page}&size=${vault.size}`
  );
  const box = $("vault-list");
  box.innerHTML = "";
  if (!r.items.length) {
    box.innerHTML = `<div class="vault-empty">${vault.q ? "没有找到匹配的词。" : "这个分类下还没有词。"}</div>`;
    $("vault-pager").innerHTML = "";
    return;
  }
  r.items.forEach((it) => {
    const st = it.status === "unseen" ? "unseen" : it.status;
    const mIdx = it.status === "unseen" ? 0 : it.status === "weak" ? 1 : it.status === "learning" ? 2 : 3;
    const row = document.createElement("div");
    row.className = "vault-row";
    row.innerHTML = `
      <span class="vw-text">
        <span class="vw-word">${titleCase(it.word)}</span>
        ${it.part_of_speech ? `<span class="vw-pos">${it.part_of_speech}</span>` : ""}
        <br><span class="vw-meaning">${it.meaning}</span>
      </span>
      <span class="vw-right">
        <span class="vw-mastery"><div class="rbar-track"><div class="rbar-fill m${mIdx}" style="width:${it.mastery}%"></div></div></span>
        <span class="vw-status s-${st}">${VAULT_STATUS_LABEL[st] || st}</span>
      </span>`;
    row.addEventListener("click", () => openWord(it.id));
    box.appendChild(row);
  });
  // 分页
  const pages = Math.max(1, Math.ceil(r.total / r.size));
  $("vault-pager").innerHTML = `
    <button class="btn btn-small" ${vault.page <= 1 ? "disabled" : ""} id="vp-prev">← 上一页</button>
    <span class="vp-info">${vault.page} / ${pages}</span>
    <button class="btn btn-small" ${vault.page >= pages ? "disabled" : ""} id="vp-next">下一页 →</button>`;
  const prev = $("vp-prev"), next = $("vp-next");
  if (prev) prev.addEventListener("click", () => { vault.page = Math.max(1, vault.page - 1); renderVaultList(); });
  if (next) next.addEventListener("click", () => { vault.page = Math.min(pages, vault.page + 1); renderVaultList(); });
}

$("vault-tabs").addEventListener("click", (e) => {
  const b = e.target.closest(".vt");
  if (!b) return;
  document.querySelectorAll(".vault-tabs .vt").forEach((x) => x.classList.remove("active"));
  b.classList.add("active");
  vault.status = b.dataset.status;
  vault.page = 1;
  renderVaultList();
});

function doVaultSearch() {
  vault.q = $("vault-q").value.trim();
  vault.page = 1;
  renderVaultList();
}
$("vault-search-btn").addEventListener("click", doVaultSearch);
$("vault-q").addEventListener("keydown", (e) => { if (e.key === "Enter") doVaultSearch(); });

// ═══════════ 词详情 ═══════════
async function openWord(wordId) {
  const w = await api(`/api/vault/word/${wordId}`);
  $("word-text").textContent = titleCase(w.word);
  $("word-pos").textContent = w.part_of_speech || "—";
  $("word-phon").textContent = w.phonetic ? `/${w.phonetic}/` : "";
  $("word-meaning").textContent = w.meaning;
  $("word-example").textContent = w.example ? `例句:${w.example}` : "暂无例句";
  const v = w.vault;
  $("word-mastery-fill").style.width = v.mastery + "%";
  $("word-mastery-num").textContent = v.mastery + "%";
  $("word-correct").textContent = v.correct_count;
  $("word-wrong").textContent = v.wrong_count;
  $("word-streak").textContent = v.current_streak;
  $("word-status").textContent = VAULT_STATUS_LABEL[v.status] || v.status;
  $("word-next").textContent =
    v.status === "mastered"
      ? (v.next_review_at ? `下次复习:${v.next_review_at}` : "已掌握,复习计划待定")
      : v.status === "unseen"
        ? "尚未学习:通过战斗或探索新词把它收进词藏"
        : "继续答对提升掌握度,达到 80% 即掌握";
  setNavActive("vault");
  showView("word");
}

$("btn-word-back").addEventListener("click", () => goNav("vault"));

$("btn-word-say").addEventListener("click", () => {
  const w = $("word-text").textContent;
  if (w) speakWord(w);
});

// ═══════════ 词藏练习 (薄弱/复习/探索) ═══════════
let practice = { active: false, mode: "weak", question: null, idx: 0, total: 0, correctCount: 0, answering: false, qShownAt: 0 };

document.querySelectorAll("[data-practice]").forEach((b) =>
  b.addEventListener("click", () => startPractice(b.dataset.practice))
);

// 练习中途返回词藏(薄弱/复习/探索新词通用)
$("btn-practice-back").addEventListener("click", () => {
  practice.active = false;
  goNav("vault");
});

async function startPractice(mode) {
  let r;
  try {
    r = await api("/api/vault/practice", { method: "POST", body: JSON.stringify({ mode }) });
  } catch (e) {
    toast(e.message);
    return;
  }
  if (r.empty) { toast(r.error); return; }
  practice = { active: true, mode, question: null, idx: 0, total: r.total, correctCount: 0, answering: false, qShownAt: 0 };
  $("practice-mode").textContent = PRACTICE_LABEL[mode];
  $("practice-empty").classList.add("hidden");
  renderPracticeQuestion(r.question);
  setNavActive("vault");
  showView("practice");
}

function renderPracticeQuestion(q) {
  practice.question = q;
  practice.answering = false;
  practice.qShownAt = Date.now();
  $("practice-progress").textContent = `${q.idx + 1} / ${q.total}`;
  $("practice-word").textContent = titleCase(q.word);
  $("practice-sub").innerHTML =
    (q.part_of_speech ? `<span class="pos">${q.part_of_speech}</span>` : "") +
    (q.phonetic ? `<span class="phon">${q.phonetic}</span>` : "");
  const box = $("practice-options");
  box.innerHTML = "";
  if (q.mode === "unseen") {
    const d = document.createElement("div");
    d.className = "practice-meaning";
    d.textContent = q.meaning;
    box.appendChild(d);
    const b = document.createElement("button");
    b.className = "btn btn-primary btn-small";
    b.textContent = "记住了,下一个 →";
    b.addEventListener("click", () => practicePick({ seen: true }));
    box.appendChild(b);
  } else {
    q.options.forEach((opt) => {
      const b = document.createElement("button");
      b.className = "quiz-opt";
      b.textContent = opt;
      b.addEventListener("click", () => practicePick({ btn: b }));
      box.appendChild(b);
    });
  }
}

async function practicePick({ btn, seen }) {
  if (practice.answering) return;
  practice.answering = true;
  const q = practice.question;
  let res;
  if (seen) {
    res = await api("/api/vault/answer", {
      method: "POST",
      body: JSON.stringify({ word_id: q.word_id, correct: true }),
    });
    practice.correctCount += 1;
    toast("已收入词藏,进入学习阶段", 1200);
  } else {
    const correct = btn.textContent === q.answer;
    const rt = (Date.now() - practice.qShownAt) / 1000;
    document.querySelectorAll("#practice-options .quiz-opt").forEach((o) => {
      o.disabled = true;
      if (o.textContent === q.answer) o.classList.add("correct");
    });
    if (!correct) btn.classList.add("wrong");
    try {
      res = await api("/api/vault/answer", {
        method: "POST",
        body: JSON.stringify({ word_id: q.word_id, correct, response_time: rt }),
      });
    } catch (e) {
      toast(e.message);
      practice.answering = false;
      return;
    }
    if (correct) practice.correctCount += 1;
  }
  setTimeout(() => {
    if (res.done) endPractice();
    else renderPracticeQuestion(res.question);
  }, seen ? 320 : 550);
}

function endPractice() {
  practice.active = false;
  toast(`练习完成:答对 ${practice.correctCount}/${practice.total}`, 2400);
  goNav("vault");
}

// ═══════════ 每日任务 ═══════════
async function renderTasks() {
  const t = await api("/api/tasks");
  const p = t.progress, g = t.goals;
  const defs = [
    { key: "monsters_killed", icon: `${ASSET}/icons/icon_sword.svg`, name: "讨伐怪物", detail: "击败今日目标数量的怪物" },
    { key: "words_learned", icon: `${ASSET}/icons/icon_book.svg`, name: "学习新词", detail: "在战斗中击破新词怪物" },
    { key: "max_combo", icon: `${ASSET}/icons/icon_fire.svg`, name: "连击高手", detail: "打出一次 ×5 以上的连击" },
    { key: "words_reviewed", icon: `${ASSET}/icons/icon_repeat.svg`, name: "复习旧词", detail: "回顾已学过的单词" },
  ];
  const list = $("task-list");
  list.innerHTML = "";
  defs.forEach((d) => {
    const cur = Math.min(p[d.key], g[d.key]);
    const done = cur >= g[d.key];
    const item = document.createElement("div");
    item.className = "task-item" + (done ? " done" : "");
    item.innerHTML = `
      <span class="task-icon"><img src="${d.icon}" alt=""></span>
      <span class="task-info">
        <span class="task-name">${d.name}</span><br>
        <span class="task-detail">${d.detail}</span>
      </span>
      <span class="task-num${done ? " done" : ""}">${cur} / ${g[d.key]}</span>`;
    list.appendChild(item);
  });
}

// ═══════════ 商店 ═══════════
async function renderShop() {
  const s = await api("/api/shop");
  $("shop-gold").textContent = user.gold;
  const box = $("shop-items");
  box.innerHTML = "";
  s.items.forEach((it) => {
    const div = document.createElement("div");
    const isSkin = it.type === "skin";
    const skinKey = isSkin ? skinKeyOf(it.name) : "";
    const usingSkin = isSkin && user.avatar === skinKey;
    div.className = "shop-item" + (it.equipped ? " equipped" : "") + (it.owned ? " owned-by-me" : "");
    let btn;
    if (isSkin) {
      // 皮肤:未拥有→购买;已拥有且当前→使用中;已拥有未用→穿戴
      btn = usingSkin
        ? `<span class="s-owned">使用中</span>`
        : it.owned
          ? `<button class="btn btn-small btn-primary skin-btn">穿戴</button>`
          : `<button class="btn btn-small btn-primary buy-btn">购买 ${it.price}<img class="mini-ico" src="${ASSET}/icons/icon_gold.svg" alt="金币"></button>`;
    } else {
      btn = it.equipped
        ? `<span class="s-owned">已装备</span>`
        : !it.owned
          ? `<button class="btn btn-small btn-primary buy-btn">购买 ${it.price}<img class="mini-ico" src="${ASSET}/icons/icon_gold.svg" alt="金币"></button>`
          : `<span class="s-owned">已拥有</span>`;
    }
    const icon = isSkin
      ? `<span class="s-icon s-skin-preview"><img src="${skinImg(it.name)}" alt=""></span>`
      : `<span class="s-icon"><img src="${itemImg(it.name)}" alt=""></span>`;
    div.innerHTML = `
      ${icon}
      <div class="s-name">${it.name_zh} <span class="s-qty">${isSkin ? "皮肤" : it.is_permanent ? "装备" : "消耗品"}</span></div>
      <div class="s-effect">${it.effect}</div>
      <div class="s-price">${it.owned ? (usingSkin ? "使用中" : "已拥有") : `<img class="mini-ico" src="${ASSET}/icons/icon_gold.svg" alt="金币"> ` + it.price}</div>
      <div class="s-actions">${btn}</div>`;
    if (isSkin) {
      const skinBtn = div.querySelector(".skin-btn");
      if (skinBtn) skinBtn.addEventListener("click", async () => {
        const r = await api("/api/skin/equip", {
          method: "POST",
          body: JSON.stringify({ item_id: it.id }),
        });
        toast(r.message);
        await refreshProfile();
        applyTheme(user.theme); // 刷新 HUD/设置页头像
        renderShop();
      });
    }
    if (!it.owned) {
      div.querySelector(".buy-btn").addEventListener("click", async () => {
        const r = await api("/api/shop/buy", {
          method: "POST",
          body: JSON.stringify({ item_id: it.id }),
        });
        toast(r.ok ? r.message : r.message);
        await refreshProfile();
        renderShop();
      });
    }
    box.appendChild(div);
  });
}

// ═══════════ 背包 ═══════════
async function renderInventory() {
  const inv = await api("/api/inventory");
  const box = $("inv-items");
  box.innerHTML = "";
  if (!inv.items.length) {
    box.innerHTML = `<p class="panel-desc">背包空空如也,去商店购置装备吧。</p>`;
    return;
  }
  inv.items.forEach((it) => {
    const div = document.createElement("div");
    const isSkin = it.type === "skin";
    const usingSkin = isSkin && user.avatar === skinKeyOf(it.name);
    div.className = "shop-item" + ((isSkin ? usingSkin : it.equipped) ? " equipped" : "");
    let action;
    if (isSkin) {
      action = `<button class="btn btn-small ${usingSkin ? "btn-danger" : "btn-primary"} skin-btn">${usingSkin ? "卸下" : "穿戴"}</button>`;
    } else {
      action = it.is_permanent
        ? `<button class="btn btn-small ${it.equipped ? "btn-danger" : "btn-primary"} equip-btn">${it.equipped ? "卸下" : "装备"}</button>`
        : `<button class="btn btn-small btn-primary use-btn">使用</button>`;
    }
    const icon = isSkin
      ? `<span class="s-icon s-skin-preview"><img src="${skinImg(it.name)}" alt=""></span>`
      : `<span class="s-icon"><img src="${itemImg(it.name)}" alt=""></span>`;
    div.innerHTML = `
      ${icon}
      <div class="s-name">${it.name_zh} <span class="s-qty">${isSkin ? "皮肤" : "×" + it.quantity}</span></div>
      <div class="s-effect">${it.effect}</div>
      <div class="s-actions">${action}</div>`;
    if (isSkin) {
      div.querySelector(".skin-btn").addEventListener("click", async () => {
        const r = await api("/api/skin/equip", {
          method: "POST",
          body: JSON.stringify({ item_id: it.id }),
        });
        toast(r.message);
        await refreshProfile();
        applyTheme(user.theme); // 刷新 HUD/设置页头像
        renderInventory();
      });
    } else if (it.is_permanent) {
      div.querySelector(".equip-btn").addEventListener("click", async () => {
        const r = await api("/api/inventory/equip", {
          method: "POST",
          body: JSON.stringify({ item_id: it.id }),
        });
        toast(r.message);
        renderInventory();
      });
    } else {
      div.querySelector(".use-btn").addEventListener("click", async () => {
        const r = await api("/api/inventory/use", {
          method: "POST",
          body: JSON.stringify({ item_id: it.id }),
        });
        toast(r.message);
        renderInventory();
      });
    }
    box.appendChild(div);
  });
}

// ═══════════ 成长 ═══════════
async function renderGrowth() {
  await refreshProfile();
  $("growth-level").textContent = "Lv." + user.level;
  $("growth-xp-fill").style.width = user.xp_percent + "%";
  $("growth-xp-text").textContent = `${user.xp_in_level} / ${user.xp_to_next} · ${user.xp_percent}%`;
  const st = await api("/api/stats");
  $("st-battles").textContent = st.total_battles;
  $("st-acc").textContent = st.accuracy + "%";
  $("st-learned").textContent = st.learned_words;
  $("st-mastered").textContent = st.mastered_words;
  const bars = [
    ["m-d1", st.mastery["1"]],
    ["m-d2", st.mastery["2"]],
    ["m-d3", st.mastery["3"]],
    ["m-d4", st.mastery["4"]],
  ];
  bars.forEach(([id, v]) => {
    $(id).style.width = v + "%";
    $(id + "-t").textContent = v + "%";
  });
}

// ═══════════ 启动 ═══════════
init();
