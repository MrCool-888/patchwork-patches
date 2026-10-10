// Injected into Blitz's main process; no code runs while Patchwork imports or previews.
function patchworkInstallDesktopCleanup(browserView, initialURL) {
  const wc = browserView.webContents;
  const contentsId = wc.id;
  const knownHosts = new Set([
    "blitz.gg", "blitzapp.gg", "agentselect.net", "championselect.net",
    "lolstats.com", "probuilds.net", "tftcomps.gg",
  ]);
  function supported(value) {
    try {
      const parsed = new URL(value);
      return parsed.protocol === "https:" && knownHosts.has(parsed.hostname) &&
        /^\/v3\.0\.8-ota\.0(?:\/|$)/.test(parsed.pathname) &&
        !parsed.searchParams.has("overlay-route");
    } catch { return false; }
  }
  const key = Symbol.for("patchwork.blitz.desktop-cleanup.v1");
  const session = wc.session;
  if (!session[key]) {
    const views = new Map();
    session[key] = { views };
    session.webRequest.onBeforeRequest({ urls: [
      "https://dn0qt3r0xannq.cloudfront.net/blitz-ONuZ1Ty9qx/*",
      "https://live.primis.tech/live/liveView.php*",
    ] }, (details, callback) => {
      const view = views.get(details.webContentsId);
      let cancel = false;
      if (view && view.active) {
        try {
          const request = new URL(details.url);
          cancel = (request.hostname === "dn0qt3r0xannq.cloudfront.net" &&
            request.pathname.startsWith("/blitz-ONuZ1Ty9qx/") &&
            request.pathname.endsWith("/prebid-load.js")) ||
            (request.hostname === "live.primis.tech" && request.pathname === "/live/liveView.php");
        } catch { /* An unrecognized URL retains its original behavior. */ }
      }
      callback({ cancel });
    });
  }
  const state = { active: supported(initialURL), cssKey: null, generation: 0 };
  session[key].views.set(contentsId, state);
  // Chromium can satisfy scripts from memory without invoking webRequest.
  // Disable cache only for supported desktop views, before their first load.
  let ownsDebugger = false;
  async function cacheMode(active) {
    try {
      if (active && !wc.debugger.isAttached()) {
        wc.debugger.attach("1.3"); ownsDebugger = true;
      }
      if (wc.debugger.isAttached()) {
        await wc.debugger.sendCommand("Network.setCacheDisabled", { cacheDisabled: active });
        if (!active && ownsDebugger) { ownsDebugger = false; wc.debugger.detach(); }
      }
    } catch (error) {
      if (!wc.isDestroyed()) log.warn("[Patchwork]", "Desktop cache bypass unavailable: " + error.message);
    }
  }
  wc.debugger.on("detach", (_event, reason) => {
    const unexpected = ownsDebugger;
    ownsDebugger = false;
    if (unexpected && state.active && !wc.isDestroyed()) log.warn("[Patchwork]", "Desktop cache bypass detached: " + reason);
  });
  wc.on("devtools-closed", () => cacheMode(supported(wc.getURL())));
  const loadURL = wc.loadURL.bind(wc);
  const reload = wc.reload.bind(wc);
  wc.loadURL = async (value, options) => {
    const bypass = cacheMode(supported(value));
    // A new view has no renderer yet; its queued protocol command needs a load.
    if (wc.getURL()) await bypass;
    if (!wc.isDestroyed()) return loadURL(value, options);
  };
  wc.reload = () => {
    if (state.active) cacheMode(true).then(() => { if (!wc.isDestroyed()) wc.reloadIgnoringCache(); }); else reload();
  };
  wc.on("will-navigate", (event, value) => {
    if (!supported(value)) return;
    event.preventDefault();
    wc.loadURL(value).catch(error => {
      if (!wc.isDestroyed()) log.warn("[Patchwork]", "Desktop navigation failed: " + error.message);
    });
  });
  const css = `
    .blitz-app { --right-rail-width: 0px !important; --rail-gap: 0px !important; }
    [class~="🤑-wrapper"], [class~="🤑-column"], [class~="🤑-leaderboard"],
    [class~="🤑-rectangle"], [class~="🤑-placeholder"],
    [data-primis-placement-id], #display-desktop-anchor, [id^="display-rr-"],
    li:has(> a.get-premium-btn), .get-premium-btn, .ads-toggle,
    .latest-feature-container.svelte-1lr1kw5, .blitz3-promo.svelte-lj7rjc {
      display: none !important; pointer-events: none !important;
    }
  `;
  function report(value) {
    if (!state.active) {
      let location = "unrecognized URL";
      try { const parsed = new URL(value); location = parsed.origin + parsed.pathname; } catch {}
      log.warn("[Patchwork]", "Desktop cleanup skipped for unsupported frontend: " + location);
    }
  }
  async function refresh(value) {
    const generation = ++state.generation;
    state.active = supported(value);
    const oldKey = state.cssKey; state.cssKey = null;
    if (oldKey && !wc.isDestroyed()) {
      try { await wc.removeInsertedCSS(oldKey); } catch {}
    }
    if (!state.active || wc.isDestroyed()) { report(value); return; }
    try {
      const inserted = await wc.insertCSS(css, { cssOrigin: "user" });
      if (wc.isDestroyed()) return;
      if (generation !== state.generation || !state.active) {
        await wc.removeInsertedCSS(inserted); return;
      }
      state.cssKey = inserted;
    } catch (error) {
      if (!wc.isDestroyed()) log.warn("[Patchwork]", "Desktop cleanup CSS failed: " + error.message);
    }
  }
  wc.on("did-start-navigation", (_event, value, inPlace, mainFrame) => {
    if (!mainFrame) return;
    state.active = supported(value);
    state.generation++;
    cacheMode(state.active);
    if (!inPlace) state.cssKey = null;
  });
  wc.on("did-finish-load", () => refresh(wc.getURL()));
  wc.on("did-navigate-in-page", (_event, value, mainFrame) => {
    if (mainFrame) refresh(value);
  });
  wc.once("destroyed", () => {
    state.active = false; state.generation++; session[key].views.delete(contentsId);
  });
  report(initialURL);
}
