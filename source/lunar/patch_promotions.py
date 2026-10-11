"""Exact edits for the inspected in-game web UI; no original bundle is distributed."""

import hashlib

UI_VERSION = 'b6d39b271abae35e95be9bdadf2270e908d59548'
UI_SHA256 = '5dfea0b2b0c331971827d97f4a8ee12b2406d005e9d5e954180ce5db328ac7db'
UI_FILE = '.lunarclient/ui/' + UI_VERSION + '/static/index.js'

# Edit callers so promotional components never mount; leave their hooks and shared controls intact.
REPLACEMENTS = [
    ('S2(O.home,y=>{l(y.buttons)})',
     'S2(O.home,y=>{l(y.buttons.filter(w=>w.id!=="settings.labels.store"&&w.id!=="gui.components.discover"))})'),
    ('i.jsx(TM2,{coins:R,onMouseDown:()=>{O.playClickSound(),O.home.theme.openStoreCoins()}})', 'null'),
    ('C1&&i.jsx(Sc0,{reward:C1,onSuccess:r1=>{n1(r1)}})', 'null'),
    ('v1&&i.jsx(jc0,{})', 'null'),
    ('(F==null?void 0:F.includes("blog"))&&u.length>0&&!e.minimal&&i.jsx(EM2,{posts:u,duration:5})', 'null'),
    ('x.includes(Gt.PROMOTION_TYPE_VIDEO)?i.jsx(Bc0,{uuid:o==null?void 0:o.uuid}):i.jsx(qa0,{showDiscordCta:p,uuid:o==null?void 0:o.uuid})', 'null'),
    ('i.jsx(Cb2,{children:i.jsx(g2,{icon:', 'false&&i.jsx(Cb2,{children:i.jsx(g2,{icon:'),
    ('showBackToStore:!0,showInstantCheckout:r!==void 0,showPreviewCosmetics:o',
     'showBackToStore:!1,showInstantCheckout:!1,showPreviewCosmetics:o'),
    ('(F==null?void 0:F.includes("radio"))&&!e.minimal&&i.jsx(XO2,', 'false&&i.jsx(XO2,'),
    ('z&&(F==null?void 0:F.includes("countdown"))&&i.jsx(Hc0,', 'false&&i.jsx(Hc0,'),
    ('(d==null?void 0:d.includes("wrapped"))&&i.jsx(yl,{title:a.year+" Wrapped"',
     'false&&i.jsx(yl,{title:a.year+" Wrapped"'),
]


def patch_ui(original):
    if hashlib.sha256(original).hexdigest() != UI_SHA256:
        raise ValueError('Unsupported or already modified in-game UI bundle.')
    text = original.decode('utf-8')
    for old, new in REPLACEMENTS:
        if text.count(old) != 1:
            raise ValueError('In-game promotion anchor is not unique: ' + old)
        text = text.replace(old, new)
    return text.encode('utf-8')
