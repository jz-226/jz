/* cloud.js — TapTap 官方云存档同步层(local-first,零后端)。

   运行环境(自动检测 window.tap 运行时):
   - 在 TapTap App 内打开 → 客户端注入 tap 运行时:
       ① tap.checkSession / tap.login 静默登录(玩家无感,无扫码、无跳转)
       ② 官方云存档 CloudSaveManager + FileSystemManager:
          本地 localStorage 是工作副本,云端是备份,按存档里的 updated_at(epoch ms)仲裁,新者胜。
          云存档自动绑定当前 TapTap 玩家 → 不需要 appid/secret,不需要自建服务器。
       ③ 玩家一打开就自动拉云端 → 换设备 / 清缓存进度都还在。
   - 纯浏览器打开 → 无 tap → 游客单机模式:存档只在 localStorage,不弹任何登录。

   同步节奏:
   - Store.persist 挂一层 → 标记 dirty → debounceMs(默认 3s)防抖后上传
   - create/updateArchive 官方限流 1 分钟 1 次 → 客户端再强制 minWriteGap 间隔,不够就等
   - 启动 + 回到前台时 pull → 比时间戳合并;云端新 → 下载并 reload 一次(让游戏用新档)
   - pagehide / visibilitychange(hidden) 兜底上传(best-effort)

   诊断:所有云端失败 console.warn + 右下角 toast(带 errMsg),绝不打断游戏。

   开发联调(本地快速测试用,线上玩家改不了):
     localStorage.setItem('wordquest_cfg', '{"minWriteGap":700,"debounceMs":100}')
   可改限流间隔 / 防抖时长。

   注意:本文件由 build_web.py 从 tools/web_js/ 复制进 web/js/,手改只在 tools/web_js/。 */
(function () {
  "use strict";

  // ── 常量 ──────────────────────────────────────────────
  const ARCHIVE_NAME = "wq_save_v1";          // 云存档名(官方要求:纯 ASCII,≤60 字节)
  const ARCHIVE_SUMMARY = "auto cloud save";  // 存档描述(必填,非空)
  const META_KEY = "wordquest_cloud_meta_v1"; // 本地记录 {uuid, fileId, lastWriteAt}

  const tap = (typeof window !== "undefined" && window.tap) || null;
  // 具备全部所需能力才启用云存档;缺任一 → 游客单机模式
  const inApp = !!(
    tap &&
    typeof tap.login === "function" &&
    typeof tap.checkSession === "function" &&
    typeof tap.getCloudSaveManager === "function" &&
    typeof tap.getFileSystemManager === "function"
  );
  const USER_PATH = (tap && tap.env && tap.env.USER_DATA_PATH) || "";
  const SAVE_FILE = USER_PATH + "/wq_save.json";   // 本地上传文件(create/update 需要真实文件路径)

  // 开发联调参数(localStorage 覆盖;默认值即线上限流)
  let cfg = { minWriteGap: 60000, debounceMs: 3000 };
  try {
    const raw = localStorage.getItem("wordquest_cfg");
    if (raw) cfg = Object.assign(cfg, JSON.parse(raw));
  } catch (e) { /* 配置损坏就忽略 */ }

  let mgr = null;            // CloudSaveManager(登录成功后惰性取,全局单例)
  let fs = null;             // FileSystemManager
  let loggedIn = false;
  let dirty = false;
  let debounceTimer = null;
  let retryTimer = null;
  let cloudInFlight = false; // 官方限制:不允许并发调用(错误码 400007)
  let meta = loadMeta();

  function loadMeta() {
    try {
      const m = JSON.parse(localStorage.getItem(META_KEY) || "{}");
      if (m && typeof m.uuid === "string" && m.uuid) return m;
    } catch (e) {}
    return {};
  }
  function saveMeta() {
    try { localStorage.setItem(META_KEY, JSON.stringify(meta)); } catch (e) {}
  }

  // ── 右下角 toast(不打断游戏) ─────────────────────────
  function showToast(msg) {
    try {
      let el = document.getElementById("cloud-toast");
      if (!el) {
        el = document.createElement("div");
        el.id = "cloud-toast";
        el.style.cssText =
          "position:fixed;left:50%;bottom:90px;transform:translateX(-50%);z-index:99999;" +
          "background:rgba(0,0,0,.85);color:#fff;padding:10px 18px;border-radius:20px;" +
          "font-size:14px;max-width:80vw;text-align:center;pointer-events:none;";
        document.body.appendChild(el);
      }
      el.textContent = msg;
      el.style.display = "block";
      clearTimeout(el._t);
      el._t = setTimeout(() => { el.style.display = "none"; }, 4000);
    } catch (e) { /* toast 失败不影响游戏 */ }
  }

  function errMsg(e) {
    return (e && (e.errMsg || e.message)) || "未知错误";
  }

  // ── 存档直写 localStorage(不走 persist,避免刚下载的档又被盖章成"新档"触发回传) ──
  function saveLocalDirect() {
    try { localStorage.setItem(SAVE_KEY, JSON.stringify(Store._s)); } catch (e) {}
  }

  // 判断本地是否有"真实进度"(防止空默认档覆盖云端真档)
  function hasRealProgress(s) {
    const u = s.user || {};
    return !!(
      u.onboarded || u.level > 1 || u.xp > 0 || u.gold > 0 ||
      (s.userWords && Object.keys(s.userWords).length) ||
      (s.inventory && Object.keys(s.inventory).length) ||
      (s.battles && s.battles.total > 0)
    );
  }

  // ── tap 调用封装:兼容回调 / Promise 两种风格,兜底超时 ──
  function callTap(fn, thisArg, params = {}) {
    return new Promise((resolve, reject) => {
      let settled = false;
      const t = setTimeout(() => { if (!settled) { settled = true; reject(new Error("tap 调用超时")); } }, 10000);
      const done = (res) => { if (!settled) { settled = true; clearTimeout(t); resolve(res || {}); } };
      const fail = (err) => { if (!settled) { settled = true; clearTimeout(t); reject(err || new Error("tap 调用失败")); } };
      try {
        const ret = fn.call(thisArg, Object.assign({}, params, { success: done, fail }));
        if (ret && typeof ret.then === "function") ret.then(done, fail);   // Promise 风格也接
      } catch (e) { fail(e); }
    });
  }

  function writeSaveFile(content) {
    return new Promise((resolve, reject) => {
      fs.writeFile({ filePath: SAVE_FILE, data: content, encoding: "utf8", success: resolve, fail: reject });
    });
  }
  function readSaveFile(filePath) {
    return new Promise((resolve, reject) => {
      fs.readFile({ filePath, encoding: "utf8", success: (res) => resolve(res && res.data), fail: reject });
    });
  }

  // 兼容不同版本的返回结构:res.saves / res.data.saves / res.data.archive_list
  function extractSaves(res) {
    if (!res) return [];
    if (Array.isArray(res.saves)) return res.saves;
    if (res.data) {
      if (Array.isArray(res.data.saves)) return res.data.saves;
      if (Array.isArray(res.data.archive_list)) return res.data.archive_list;
    }
    return [];
  }

  function archiveMetaData() {
    return { name: ARCHIVE_NAME, summary: ARCHIVE_SUMMARY };
  }

  // 云存档就绪?(惰性取 manager / fs,未登录不取)
  function cloudReady() {
    if (!inApp || !loggedIn) return false;
    if (!mgr) { try { mgr = tap.getCloudSaveManager(); } catch (e) {} }
    if (!fs) { try { fs = tap.getFileSystemManager(); } catch (e) {} }
    return !!(mgr && fs);
  }

  // ── 上传:本地存档 → 写入本地文件 → create/updateArchive ──
  async function doUpload() {
    if (!cloudReady()) return;
    if (!hasRealProgress(Store._s)) return;   // 空默认档不上传,防把脏档传到云上
    dirty = false;                            // 先摘脏标记(防 pagehide flush 重复发送)
    try {
      await writeSaveFile(JSON.stringify(Store._s));
      if (meta.uuid) {
        const res = await callTap(mgr.updateArchive, mgr, {
          archiveUUID: meta.uuid,
          archiveMetaData: archiveMetaData(),
          archiveFilePath: SAVE_FILE,
        });
        if (res && res.fileId) { meta.fileId = res.fileId; saveMeta(); }   // update 后 fileId 会更新
      } else {
        const res = await callTap(mgr.createArchive, mgr, {
          archiveMetaData: archiveMetaData(),
          archiveFilePath: SAVE_FILE,
        });
        meta.uuid = (res && res.uuid) || "";
        meta.fileId = (res && res.fileId) || "";
        if (meta.uuid) saveMeta();
        else await findArchiveByName();   // 创建成功但响应没拿到 uuid → 从列表兜底找回
      }
      meta.lastWriteAt = Date.now();
      saveMeta();
    } catch (e) {
      dirty = true;
      const msg = errMsg(e);
      console.warn("[cloud] 云存档上传失败", msg);
      showToast("云存档上传失败:" + msg.slice(0, 60));
      clearTimeout(retryTimer);
      retryTimer = setTimeout(tryUpload, cfg.minWriteGap);   // 失败后自动重试一次
    }
  }

  // 从存档列表按名字找回 uuid(创建成功但响应丢失 uuid 时自愈)
  async function findArchiveByName() {
    try {
      const res = await callTap(mgr.getArchiveList, mgr);
      const mine = extractSaves(res).find((a) => a && a.name === ARCHIVE_NAME);
      if (mine) { meta.uuid = mine.uuid; meta.fileId = mine.fileId || ""; saveMeta(); }
    } catch (e) { /* 忽略 */ }
  }

  // 上传入口:防抖 / 限流 / 并发三重闸门
  async function tryUpload() {
    if (!loggedIn || !dirty) return;
    if (cloudInFlight) { clearTimeout(retryTimer); retryTimer = setTimeout(tryUpload, 2000); return; }
    const waitMs = (meta.lastWriteAt || 0) + cfg.minWriteGap - Date.now();
    if (waitMs > 0) { clearTimeout(retryTimer); retryTimer = setTimeout(tryUpload, waitMs + 200); return; }
    await doUpload();
  }

  // 每次落盘 → 安排上传(防抖)
  function scheduleUpload() {
    if (!inApp || !loggedIn) return;
    dirty = true;
    clearTimeout(debounceTimer);
    debounceTimer = setTimeout(tryUpload, cfg.debounceMs);
  }

  // ── 拉取:读云存档 → 合并(新者胜) ──
  async function pull() {
    if (!cloudReady()) return "idle";
    if (cloudInFlight) return "idle";
    cloudInFlight = true;
    try {
      let mine = null;
      try {
        const listRes = await callTap(mgr.getArchiveList, mgr);
        mine = extractSaves(listRes).find((a) => a && a.name === ARCHIVE_NAME) || null;
      } catch (e) {
        console.warn("[cloud] 读取云存档列表失败", errMsg(e));
        return "idle";
      }
      if (!mine) {
        if (hasRealProgress(Store._s)) { await doUpload(); return "upload"; }
        return "idle";
      }
      meta.uuid = mine.uuid;
      meta.fileId = mine.fileId || "";
      saveMeta();
      let filePath = "";
      try {
        const dRes = await callTap(mgr.getArchiveData, mgr, { archiveUUID: mine.uuid, archiveFileId: mine.fileId });
        filePath = (dRes && dRes.filePath) || "";
      } catch (e) {
        console.warn("[cloud] 下载云端存档失败", errMsg(e));
        return "idle";
      }
      if (!filePath) return "idle";
      let raw = "";
      try { raw = (await readSaveFile(filePath)) || ""; } catch (e) {
        console.warn("[cloud] 读取云端存档文件失败", errMsg(e));
        return "idle";
      }
      let remote = null;
      try { remote = JSON.parse(raw); } catch (e) { /* 云端数据损坏 → 视为无档 */ }
      if (!remote || typeof remote !== "object") return "idle";
      return await applyMerge(remote);
    } finally {
      cloudInFlight = false;
    }
  }

  // 合并云端与本地,返回 "download" | "upload" | "idle"
  // 顺序很重要:
  //   ① 云端无档 → 本地有真实进度才上传(首登把游客档带上云)
  //   ② 本地是刚生成的空默认档(无真实进度)→ 云端有真档才接管。
  //      必须先查这个:空档的 updated_at 是"现在",不查会把云端真档覆盖成空档。
  //   ③ 双方都有进度 → 比 updated_at,新者胜;云端若是空档则一律以本地为准
  async function applyMerge(remote) {
    if (!remote) {
      if (hasRealProgress(Store._s)) { await doUpload(); return "upload"; }
      return "idle";
    }
    if (!hasRealProgress(Store._s)) {
      if (hasRealProgress(remote)) {
        Store._s = remote;
        saveLocalDirect();
        return "download";
      }
      return "idle";   // 两边都空 → 不动,防 download→reload 死循环
    }
    const localTs = Store._s.updated_at || 0;
    const remoteTs = remote.updated_at || 0;
    if (remoteTs > localTs) {
      if (hasRealProgress(remote)) {
        Store._s = remote;
        saveLocalDirect();
        return "download";
      }
      await doUpload();   // 云端是空档而本地有真进度 → 本地上传覆盖
      return "upload";
    }
    if (localTs > remoteTs) { await doUpload(); return "upload"; }
    return "idle";   // 时间戳相等 → 不动
  }

  // 兜底:页面即将关闭 / 切后台 → 尽力上传一次(可能赶不上,主力靠防抖)
  function flush() {
    if (!inApp || !loggedIn || !dirty) return;
    if (!cloudReady()) return;
    if ((meta.lastWriteAt || 0) + cfg.minWriteGap - Date.now() > 0) return;   // 刚写过,跳过
    dirty = false;   // pagehide 与 visibilitychange 会双触发,先摘脏标记防重复发送
    try {
      fs.writeFile({
        filePath: SAVE_FILE,
        data: JSON.stringify(Store._s),
        encoding: "utf8",
        success: () => {
          try {
            if (meta.uuid) {
              mgr.updateArchive({ archiveUUID: meta.uuid, archiveMetaData: archiveMetaData(), archiveFilePath: SAVE_FILE, fail: () => { dirty = true; } });
            } else {
              mgr.createArchive({ archiveMetaData: archiveMetaData(), archiveFilePath: SAVE_FILE, fail: () => { dirty = true; } });
            }
          } catch (e) {}
        },
        fail: () => { dirty = true; },
      });
    } catch (e) { dirty = true; }
  }

  // ── 静默登录:已有会话跳过,否则 tap.login(无感,不弹任何 UI) ──
  async function ensureLoggedIn() {
    try {
      await callTap(tap.checkSession, tap, {});
      return true;
    } catch (e) {
      try { await callTap(tap.login, tap, {}); return true; } catch (e2) {
        console.warn("[cloud] TapTap 静默登录失败", errMsg(e2));
        return false;
      }
    }
  }

  // 云存档由 TapTap 官方运行时绑定当前账号,游戏内无法解绑 → 登录按钮无意义,只提示
  function logout() {
    if (inApp) showToast("云存档已绑定 TapTap 账号,自动同步中,无需退出登录");
  }

  function authMe() {
    const u = Store.getUser();
    return { logged_in: loggedIn, username: loggedIn ? (u.username || "") : "" };
  }

  // ── 启动 ──────────────────────────────────────────────
  async function boot() {
    if (!inApp) {
      console.log("[cloud] 浏览器环境:游客单机模式,存档仅在本机");
      return;
    }
    Store.load();
    if (!(await ensureLoggedIn())) {
      showToast("TapTap 登录失败,存档仅保存在本机");
      return;
    }
    loggedIn = true;
    // 直接写 username,不走 persist:避免把本地存档盖章成"当前时间新档",
    // 否则一打开就永远压过云端真档(多设备本地优先的最大坑)
    Store._s.user.username = "TapTap 玩家";
    saveLocalDirect();
    const result = await pull();
    if (result === "download") {
      showToast("已连接云端,恢复最新进度");
      setTimeout(() => location.reload(), 400);   // 云端档覆盖了本地,重启让游戏用新档
    } else {
      showToast("已自动登录 TapTap · 存档云端同步");
    }
  }

  // ── 挂到 Store.persist:每次落盘都顺手安排云端上传 ──
  const _origPersist = Store.persist.bind(Store);
  Store.persist = function () {
    _origPersist();
    scheduleUpload();
  };

  // 退出 / 切后台前兜底上传;回到前台时拉一次云端(换设备后回前台也能拿到最新档)
  window.addEventListener("pagehide", flush);
  document.addEventListener("visibilitychange", () => {
    if (document.visibilityState === "hidden") {
      flush();
    } else if (document.visibilityState === "visible" && inApp && loggedIn) {
      pull().then((r) => { if (r === "download") setTimeout(() => location.reload(), 400); });
    }
  });

  // 暴露给 engine.js(退出登录 / 登录态查询)与调试
  window.CloudSync = {
    boot, logout, authMe, pull, showToast,
    status: () => ({ inApp, loggedIn, dirty, uuid: meta.uuid || "", fileId: meta.fileId || "", lastWriteAt: meta.lastWriteAt || 0 }),
  };

  boot();
})();
