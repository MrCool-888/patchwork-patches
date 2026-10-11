// Evaluate only inspected component functions with local React/bridge stand-ins; no browser or network.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const [originalFile, patchedFile] = process.argv.slice(2);
const original = fs.readFileSync(originalFile, 'utf8');
const patched = fs.readFileSync(patchedFile, 'utf8');
new vm.Script(original);
new vm.Script(patched);

function component(text, name, endMarker, context) {
  const start = text.indexOf(name + '=');
  assert(start >= 0 && text.indexOf(name + '=', start + 1) === -1);
  const end = text.indexOf(endMarker, start);
  assert(end > start);
  const body = text.slice(start + name.length + 1, end);
  for (const match of body.matchAll(/i\.jsx[s]?\(([A-Za-z0-9_$]+)[,{]/g))
    if (!(match[1] in context)) context[match[1]] = match[1];
  return vm.runInNewContext('(' + body + ')', context);
}

function harness(pendingReward = false, video = true, isMainMenu = true) {
  let cursor = 0;
  const slots = [], clicks = [], calls = [];
  const state = {auth: {accounts: [], addingAccount: false, loggingIn: false},
    assetsclient: {state: 'ready', coins: 100, onlineStatus: 'online'},
    storePreview: {previewEntry: {id: 42}, previewEntries: [{id: 42}]}};
  const O = {outfit: {}, lunarPlus: {}, promotion: {},
    home: {alerts: {}, blogPosts: {}, navigation: {}, theme: {}, radio: {}, social: {}},
    onClick: id => clicks.push(id), playClickSound: () => {},
    setScreen: id => clicks.push(id), cosmeticPreview: {
      exitPreview: () => clicks.push('exit-preview'), openStore: () => clicks.push('store'),
      openPreviewModal: () => clicks.push('preview'), checkout: () => clicks.push('checkout')
    }};
  const buttons = ['gui.components.singleplayer', 'gui.components.multiplayer',
    'gui.components.discover', 'settings.labels.store'].map(id => ({id}));
  const nav = ['gui.components.btnLocker', 'gui.components.btnOptions'].map(id => ({id}));
  const payloads = new Map([
    [O.home, {buttons}],
    [O.outfit, {outfits: [], defaultOutfitId: 'default'}],
    [O.lunarPlus, {lunarPlus: {isLunarPlus: false}}],
    [O.home.alerts, {alerts: [{id: 'service', icon: 'info', text: 'Service status'}], dismissed: []}],
    [O.home.blogPosts, {blogPosts: [{imageUrl: 'local-news', title: 'News', link: 'local'}]}],
    [O.home.navigation, nav],
    [O.home.theme, {themes: [{name: 'default', additions: []}], theme: 'default',
      additions: ['blog', 'radio', 'countdown', 'wrapped', 'discordCta'], year: 2026}],
    [O.home.radio, {currentSong: 'song', musicPlaying: true, volume: 1}],
    [O.home.social, {connected: true, socials: []}],
    [O.promotion, {promotions: video ? ['video', 'medal'] : ['medal'],
      pendingRewards: pendingReward ? [{type: 'medal'}] : []}]
  ]);
  const context = {
    i: {Fragment: 'Fragment', jsx: (type, props) => { const call = {type, props}; calls.push(call); return call; }},
    g: {
      useState: initial => { const index = cursor++; if (!(index in slots)) slots[index] = initial;
        return [slots[index], value => { slots[index] = typeof value === 'function' ? value(slots[index]) : value; }]; },
      useRef: value => ({current: value}), useMemo: fn => fn(), useEffect: () => {},
      useContext: () => ({setActiveTour: () => {}})
    },
    R6: () => ({isMainMenu, vanillaHome: false}), J1: selector => selector(state),
    s6: () => id => id, c4: () => undefined, Ia: () => ({minecraftVersionStr: 'test', version: 'test'}),
    S2: (resource, fn) => { if (payloads.has(resource)) fn(payloads.get(resource)); },
    ch: fn => fn, O, C0: {ONLINE: 'online'}, L0: {Online: 'online'}, M3: {READY: 'ready', DISCONNECTED: 'disconnected'},
    Gt: {PROMOTION_TYPE_VIDEO: 'video', PROMOTION_TYPE_MEDAL: 'medal'}, Lk: {PENDING_REWARD_TYPE_MEDAL: 'medal'},
    hO: () => 'icon', Xa0: 'local-background', Xe: {}, $3: {}, A6: 'local-logo',
    xt: 'icon', Jo: 'icon', qc0: 'icon', Xc0: 'icon', _H: 'icon', lr: {}, V: {}, $: () => 'icon',
    wk: () => ({returnToGame: 'return', checkout: 'checkout', backToStore: 'store',
      exitPreview: 'exit', previewCosmetics: 'preview'}), U3: {NULL: 'return-game'}
  };
  context.i.jsxs = context.i.jsx;
  return {context, clicks, calls, buttons, nav, render(fn, props) {
    cursor = 0; fn(props); // Simulate the next React render after bridge payload state updates.
    cursor = 0; calls.length = 0; return fn(props);
  }};
}

for (const reward of [false, true]) {
  const before = harness(reward), after = harness(reward);
  before.render(component(original, 'dh', ',Bc0=', before.context), {children: 'play'});
  after.render(component(patched, 'dh', ',Bc0=', after.context), {children: 'play'});
  for (const name of ['TM2', 'EM2', 'XO2', 'Hc0', reward ? 'Sc0' : 'jc0']) {
    assert(before.calls.some(x => x.type === name), 'Original control fixture missing ' + name);
    assert(!after.calls.some(x => x.type === name), 'Promotion still mounts: ' + name);
  }
  assert(after.calls.some(x => x.type === 'kR2'), 'Theme selector removed');
  assert(after.calls.some(x => x.type === 'wJ' && x.props.text === 'Service status'), 'Ordinary alert removed');
  const nav = after.calls.find(x => x.type === 'kc0');
  assert.deepEqual(Array.from(nav.props.navButtons, x => x.id), after.nav.map(x => x.id));
  nav.props.onButtonClick('gui.components.btnLocker');
  assert(after.clicks.includes('gui.components.btnLocker'), 'Locker click path removed');
}

for (const video of [false, true]) {
  const before = harness(false, video), after = harness(false, video);
  before.render(component(original, 'LH', ';var $w=', before.context), {wrapped: false});
  after.render(component(patched, 'LH', ';var $w=', after.context), {wrapped: false});
  const oldButtons = before.calls.filter(x => x.type === 'Z02');
  const buttons = after.calls.filter(x => x.type === 'Z02');
  assert.equal(oldButtons.length, 4);
  assert.deepEqual(buttons.map(x => x.props.text), ['gui.components.singleplayer', 'gui.components.multiplayer']);
  for (const button of buttons) button.props.onMouseDown();
  assert.deepEqual(after.clicks, ['gui.components.singleplayer', 'gui.components.multiplayer']);
  assert(before.calls.some(x => x.type === (video ? 'Bc0' : 'qa0')));
  assert(!after.calls.some(x => ['Bc0', 'qa0', 'yl'].includes(x.type)), 'Reward/Wrapped card still mounts');
}

for (const text of [original, patched]) {
  const h = harness();
  h.render(component(text, 'rd0', ',nd0=', h.context), {children: 'local-outfit', shopLink: 'local-shop', shopText: 'Shop Cloaks'});
  assert(h.calls.some(x => x.type === 'lb2' && x.props.children === 'local-outfit'));
  assert.equal(h.calls.some(x => x.type === 'Cb2'), text === original, 'Locker shop control mismatch');
}

(async () => {
  for (const text of [original, patched]) {
    const h = harness();
    const result = h.render(component(text, 'Ba0', ';var yk=', h.context));
    assert.equal(result.props.showBackToStore, text === original);
    assert.equal(result.props.showInstantCheckout, text === original);
    assert(result.props.showPreviewCosmetics);
    await result.props.onExit(); await result.props.onReturnToGame(); await result.props.onPreviewCosmetics();
    assert.deepEqual(h.clicks, ['exit-preview', 'return-game', 'preview']);
  }
  const h = harness(false, false, false);
  const result = h.render(component(patched, 'dh', ',Bc0=', h.context), {children: 'game-screen'});
  assert.equal(result.props.children, 'game-screen', 'Non-home game screen changed');
  console.log('PASS actual UI component paths: promotions do not mount; play, Locker, theme, service alert and preview exit remain.');
})().catch(error => { console.error(error); process.exitCode = 1; });
