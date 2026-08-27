/* meSpeak XHR 垫片(v1.0.3)
 *
 * 问题:meSpeakCore 加载语音走 `new XMLHttpRequest()` + 相对路径 XHR,在 file://、
 * 自定义协议 WebView(TapTap 内置等)、或受 CSP 约束的环境里会被浏览器拦掉,导致
 * 发音引擎报"不可用"。
 *
 * 本垫片在加载 mespeak-core.js 之前注入:拦截所有指向语音 JSON 的 GET,直接从内存
 * 全局 __WQ_VOICE_JSON(由 voices-en-us.js 提供)应答,一次网络请求都不发。
 * 非语音请求原样委托给原生 XMLHttpRequest(本游戏其它地方只用 fetch,不会撞车)。
 */
(function () {
  if (window.__wqMespeakShim) return;
  window.__wqMespeakShim = true;

  var NativeXHR = window.XMLHttpRequest;

  function WQXHR() {
    var self = this;
    this._url = "";
    this._native = null;
    this.readyState = 0;
    this.status = 0;
    this.responseText = "";
    this.onreadystatechange = null;

    this.open = function (method, url) {
      self._url = String(url);
    };
    this.setRequestHeader = function () {};
    this.abort = function () {
      if (self._native) self._native.abort();
    };

    this.send = function () {
      var json = window.__WQ_VOICE_JSON;
      // 语音文件特征 URL:结尾 voices/en/en-us.json(后缀匹配,不依赖路径解析;
      // 核心按相对路径请求,不要在前面加 mespeak 前缀否则匹配不上)
      if (json && /voices[\\/]en[\\/]en-us\.json$/i.test(self._url)) {
        self.readyState = 4;
        self.status = 200;
        self.responseText = json;
        if (self.onreadystatechange) self.onreadystatechange();
        return;
      }
      // 其余请求交给原生 XHR
      try {
        var n = self._native = new NativeXHR();
        n.open("GET", self._url);
        n.onreadystatechange = function () {
          self.readyState = n.readyState;
          self.status = n.status;
          self.responseText = n.responseText;
          if (self.onreadystatechange) self.onreadystatechange();
        };
        n.send();
      } catch (e) {
        self.readyState = 4;
        self.status = 0;
        self.responseText = "";
        if (self.onreadystatechange) self.onreadystatechange();
      }
    };
  }

  window.XMLHttpRequest = WQXHR;
})();
