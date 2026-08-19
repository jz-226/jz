/* cloud.js — 云存档同步层(本地优先 local-first)。

   职责:把 store.js 的 localStorage 存档同步到 Flask 云后端,身份走 TapTap 登录。
   - 本地 localStorage 永远是工作副本,云端是备份 / 跨设备同步
   - 有 token(已登录)才联网;游客(无 token)全程零网络请求,纯单机
   - 冲突仲裁:比较存档里的 updated_at(epoch 毫秒),新者胜

   配置优先级:localStorage 的 wordquest_cfg(JSON)覆盖默认,方便本地联调:
     localStorage.setItem('wordquest_cfg', '{"api":"http://127.0.0.1:5001/api/v1"}')
   部署时直接把 DEFAULT_CFG.api 改成生产地址。

   注意:本文件由 build_web.py 从 tools/web_js/ 复制进 web/js/,手改只在 tools/web_js/。 */
(function () {
  "use strict";

  // ── 配置 ──────────────────────────────────────────────
  const DEFAULT_CFG = {
    api: "",          // 后端地址(/api/v1),部署后填 https://你的域名/api/v1
    client_id: "",    // TapTap Client ID,开发者中心开启登录服务后获取
    is_cn: true,      // 中国大陆区(true) / 海外(false)
  };
  const TOKEN_KEY = "wordquest_token_v1";
  const STATE_KEY = "wordquest_oauth_state";
  const CFG_KEY = "wordquest_cfg";

  function loadConfig() {
    let cfg = Object.assign({}, DEFAULT_CFG);
    try {
      const raw = localStorage.getItem(CFG_KEY);
      if (raw) cfg = Object.assign(cfg, JSON.parse(raw));
    } catch (e) { /* 配置损坏就忽略 */ }
    return cfg;
  }

  const cfg = loadConfig();
  const ACCOUNTS_HOST = cfg.is_cn ? "https://accounts.tapapis.cn" : "https://accounts.tapapis.com";

  let token = "";
  let dirty = false;     // 有未上传的本地改动
  let timer = null;      // 防抖定时器
  let lastRemoteTs = 0;  // 上次确认的云端时间戳

  const enabled = () => !!cfg.api;    // 没配后端地址 → 整层关闭(部署前的安全状态)
  const loggedIn = () => !!token;

  // ── HTTP ──────────────────────────────────────────────
  async function api(path, opts = {}) {
    const headers = { "Content-Type": "application/json" };
    if (token) headers["Authorization"] = "Bearer " + token;
    const res = await fetch(cfg.api + path, { headers, ...opts });
    let data = null;
    try { data = await res.json(); } catch (e) { /* 非 JSON 响应 */ }
    if (!res.ok) {
      const err = new Error((data && data.error) || ("请求失败 " + res.status));
      err.status = res.status;
      throw err;
    }
    return data;
  }

  // ── 存档直写(不走 persist,避免刚下载的档又被盖章成"新档"触发回传) ──
  function saveLocalDirect() {
    try { localStorage.setItem(SAVE_KEY, JSON.stringify(Store._s)); } catch (e) { /* 内存兜底 */ }
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

  // ── 上传 / 拉取 ───────────────────────────────────────
  async function upload() {
    if (!loggedIn()) return;
    dirty = false;
    try {
      const res = await api("/save", { method: "PUT", body: JSON.stringify({ save: Store._s }) });
      lastRemoteTs = res.save_updated_at || 0;
    } catch (e) {
      dirty = true;   // 失败标记为脏,下次 persist 自动重试
      console.warn("[cloud] 上传失败", e.message || e);
    }
  }

  function scheduleUpload() {
    if (!loggedIn()) return;
    dirty = true;
    clearTimeout(timer);
    timer = setTimeout(upload, 3000);   // 3 秒防抖:一次战斗连发多个 persist 只传一次
  }

  // 合并远端存档与本地。返回 "download" | "upload" | "idle"
  // 顺序很重要:
  //   ① 云端无档 → 本地有真实进度才上传(首登把游客档带上云)
  //   ② 本地是刚生成的空默认档(无真实进度)→ 直接接管云端档。
  //      必须先查这个:空档的 updated_at 是"现在",不查会把云端真档覆盖成空档。
  //   ③ 双方都有进度 → 比 updated_at,新者胜
  async function applyMerge(remote) {
    if (!remote) {
      if (hasRealProgress(Store._s)) { await upload(); return "upload"; }
      return "idle";
    }
    if (!hasRealProgress(Store._s)) {
      Store._s = remote;
      saveLocalDirect();
      lastRemoteTs = remote.updated_at || 0;
      return "download";
    }
    const localTs = Store._s.updated_at || 0;
    const remoteTs = remote.updated_at || 0;
    if (remoteTs > localTs) {
      // 云端新 → 整个换成本地工作副本,直写 localStorage
      Store._s = remote;
      saveLocalDirect();
      lastRemoteTs = remoteTs;
      return "download";
    }
    if (localTs > remoteTs) { await upload(); return "upload"; }
    return "idle";   // 时间戳相等 → 不动
  }

  // 启动拉取 + 合并
  async function sync() {
    Store.load();
    const me = await api("/me");
    return applyMerge(me.save);
  }

  function flush() {
    if (!loggedIn() || !dirty) return;
    // keepalive:页面即将关闭也能把请求发出去(约 64KB 上限,兜底只覆盖最后几秒,主力靠防抖)
    api("/save", { method: "PUT", body: JSON.stringify({ save: Store._s }), keepalive: true })
      .catch((e) => console.warn("[cloud] 退出前上传失败", e.message || e));
  }

  // ── 登录 / 登出 ───────────────────────────────────────
  function loginUrl() {
    const state = Math.random().toString(36).slice(2);
    try { localStorage.setItem(STATE_KEY, state); } catch (e) {}
    const redirect = location.origin + location.pathname;
    const p = new URLSearchParams({
      client_id: cfg.client_id,
      response_type: "code",
      redirect_uri: redirect,
      scope: "public_profile",
      state,
    });
    return ACCOUNTS_HOST + "/oauth2/v1/authorize?" + p.toString();
  }

  async function loginWithCode(code) {
    const res = await api("/auth/taptap", { method: "POST", body: JSON.stringify({ code }) });
    token = res.token;
    try { localStorage.setItem(TOKEN_KEY, token); } catch (e) {}
    // 登录态 trick:username 一设,app.js 的设置页自动显示"已登录 @昵称"+ 退出按钮
    Store.patchUser({ username: res.user.nickname });
    // 首次同步:合并云端档与本地(空默认档会直接接管云端档,有真实进度则上传)
    Store.load();
    await applyMerge(res.save);
    return res;
  }

  function logout() {
    if (token) api("/auth/logout", { method: "POST" }).catch(() => {});
    token = "";
    try { localStorage.removeItem(TOKEN_KEY); } catch (e) {}
    Store.patchUser({ username: "" });   // 反向 trick:登录态消失
    dirty = false;
    clearTimeout(timer);
    renderAuthUI();   // 重新显示登录按钮
  }

  function authMe() {
    if (!token) return { logged_in: false };
    const u = Store.getUser();
    return { logged_in: true, username: u.username || "", user_id: u.id };
  }

  // ── 登录按钮注入(运行时,不改模板) ───────────────────
  function renderAuthUI() {
    if (!cfg.client_id) return;   // 没配 Client ID 不显示登录入口
    ensureInjected();
    const logged = loggedIn();
    const wel = document.getElementById("btn-ttlogin");
    if (wel) wel.classList.toggle("hidden", logged);
    const set = document.getElementById("btn-ttlogin-settings");
    if (set) set.classList.toggle("hidden", logged);
  }

  function ensureInjected() {
    // 欢迎页:在"开始远征"按钮后面插一个
    const start = document.getElementById("btn-start");
    if (start && !document.getElementById("btn-ttlogin")) {
      const b = document.createElement("button");
      b.id = "btn-ttlogin";
      b.className = "btn btn-small";
      b.textContent = "TapTap 登录";
      b.style.marginLeft = "10px";
      b.addEventListener("click", () => { location.href = loginUrl(); });
      start.parentNode.insertBefore(b, start.nextSibling);
    }
    // 设置页:账号行的"退出登录"按钮前插一个
    const logoutBtn = document.getElementById("btn-logout");
    if (logoutBtn && !document.getElementById("btn-ttlogin-settings")) {
      const b = document.createElement("button");
      b.id = "btn-ttlogin-settings";
      b.className = "btn btn-small";
      b.style.marginLeft = "8px";
      b.textContent = "TapTap 登录";
      b.addEventListener("click", () => { location.href = loginUrl(); });
      logoutBtn.parentNode.insertBefore(b, logoutBtn);
    }
  }

  // ── 启动 ──────────────────────────────────────────────
  async function boot() {
    if (!enabled()) return;   // 未配置后端地址 → 整层关闭
    token = "";
    try { token = localStorage.getItem(TOKEN_KEY) || ""; } catch (e) {}

    // ① TapTap OAuth 回调:URL 带 code
    const params = new URLSearchParams(location.search);
    const code = params.get("code");
    const state = params.get("state");
    if (code) {
      history.replaceState(null, "", location.pathname + location.hash);   // 清掉 code,防刷新重复兑换
      let savedState = null;
      try { savedState = localStorage.getItem(STATE_KEY); localStorage.removeItem(STATE_KEY); } catch (e) {}
      if (state && savedState && state !== savedState) {
        console.warn("[cloud] state 校验失败,忽略回调");   // 防 CSRF
      } else {
        try {
          await loginWithCode(code);
          setTimeout(() => location.reload(), 200);   // 干净重启一次,进入登录态
          return;
        } catch (e) {
          console.warn("[cloud] TapTap 登录失败", e.message || e);   // 保持游客状态继续玩
        }
      }
    }

    // ② 已有 token → 拉云端同步
    if (token) {
      try {
        const result = await sync();
        if (result === "download") {
          setTimeout(() => location.reload(), 200);   // 云端档覆盖了本地,重启让游戏用新档
          return;
        }
      } catch (e) {
        if (e.status === 401) {
          token = "";
          try { localStorage.removeItem(TOKEN_KEY); } catch (_) {}
        }
        console.warn("[cloud] 同步失败,继续本地游玩", e.message || e);
      }
    }

    renderAuthUI();
  }

  // ── 挂到 Store.persist:每次落盘都顺手安排云端上传 ──
  const _origPersist = Store.persist.bind(Store);
  Store.persist = function () {
    _origPersist();
    scheduleUpload();
  };

  // 退出 / 切后台前兜底上传
  window.addEventListener("pagehide", flush);
  document.addEventListener("visibilitychange", () => {
    if (document.visibilityState === "hidden") flush();
  });

  // 暴露给 engine.js(退出登录 / 登录态查询)与调试
  window.CloudSync = {
    boot, loginUrl, logout, authMe, sync,
    status: () => ({ loggedIn: loggedIn(), dirty, api: cfg.api, client_id: cfg.client_id }),
  };

  boot();
})();
