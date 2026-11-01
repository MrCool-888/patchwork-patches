import base64
import argparse
import hashlib
import json
import struct
from pathlib import Path

parser = argparse.ArgumentParser(description='Build the Lunar 3.7.29 pack from the 0.1.6 3.7.24 pack and exact installed launcher originals.')
parser.add_argument('--old-pack', type=Path, required=True)
parser.add_argument('--launcher-dir', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
original = args.old_pack
install = args.launcher_dir
asar = (install / 'resources/app.asar').read_bytes()
exe = (install / 'Lunar Client.exe').read_bytes()
sha = lambda b: hashlib.sha256(b).hexdigest()
assert sha(original.read_bytes()) == '27d12b250e74d7ec80628d945f25ad586e193781040c6ba38f1e7d750d2c62af'
assert sha(asar) == '8066329e2849786e878e1fe62e436fd4d219ed3d4890fe8d4f049ef95403a0a1'
assert sha(exe) == '83718a352c855697bd0e456d2111f3f921bf7bdef54a5f069b835452d3ed8459'
assert struct.unpack_from('<I', asar)[0] == 4
header_size = struct.unpack_from('<I', asar, 4)[0]
header = json.loads(asar[16:16 + struct.unpack_from('<I', asar, 12)[0]])
def entry(name):
    item = header
    for part in name.split('/'):
        item = item['files'][part]
    start = 8 + header_size + int(item['offset'])
    return asar[start:start + item['size']]

pack = json.loads(original.read_text(encoding='utf-8'))
pack.update(id='lunar-local-3-7-29-ow', appVersion='3.7.29-ow / multiver 29a43e6', packVersion='0.1.7', versionSha256=sha(asar))
patches = {p['id']: p for p in pack['patches']}
clean = patches['clean-launcher']['operations']
sentinel = b'dL7pKGdnNz796PbbjQWNKmHXBZaB9tsX'
assert exe.count(sentinel) == 1
wire = exe.index(sentinel) + len(sentinel)
assert exe[wire:wire+10] == b'\x01\x0910111101'
offset = wire + 6
changed = exe[:offset] + b'0' + exe[offset+1:]
clean[0].update(sha256=sha(exe), resultSha256=sha(changed), edits=[{'offset':offset,'length':1,'dataBase64':base64.b64encode(b'0').decode()}])

# Each exact replacement was mapped to its corresponding 3.7.29 component.
edits = [
('return{alert:nXe(e.alert),blogPosts:rXe(e.blogPosts,W9.getReadPosts()),serversCta:e.serversCta,servers:e.shelfServers,navItems:e.navItems,videoPromotion:e.videoPromotion,ads:e.ads,theme:`default`,carousel:e.carousel,announcements:e.announcements}', 'return{alert:void 0,blogPosts:[],serversCta:void 0,servers:[],navItems:[],videoPromotion:void 0,ads:[],theme:`default`,carousel:[],announcements:[]}'),
('Sn=e=>{let t=x(e=>e.metadata.ads)', 'Sn=e=>{return false;let t=x(e=>e.metadata.ads)'),
('bn=e=>{let t=Yt(),n=x(e=>e.metadata.ads)', 'bn=e=>{return null;let t=Yt(),n=x(e=>e.metadata.ads)'),
('onHighImpactRemoved:c})=>{let[l,u]=(0,V.useState)', 'onHighImpactRemoved:c})=>{return null;let[l,u]=(0,V.useState)'),
('CG=()=>{let{t:e}=i(),t=F(e=>e.metadata.layoutMetadataLoaded)', 'CG=()=>{return null;let{t:e}=i(),t=F(e=>e.metadata.layoutMetadataLoaded)'),
('mK=()=>{let{t:e}=i(),[t,n]=(0,V.useState)(!1),r=()=>{window.lunar.misc.openExternalLink({url:cK', 'mK=()=>{return null;let{t:e}=i(),[t,n]=(0,V.useState)(!1),r=()=>{window.lunar.misc.openExternalLink({url:cK'),
('cv=()=>{let{t:e}=i(),t=F(e=>e.styngr.price)', 'cv=()=>{return null;let{t:e}=i(),t=F(e=>e.styngr.price)'),
('videoPromotion:r,promotionEligible:a,t:e}),[e,n,r,t,a]', 'videoPromotion:r,promotionEligible:a,t:e}).filter(e=>e.type!==`external`&&e.type!==`promotion`),[e,n,r,t,a]'),
('DN=e=>{let{t}=n();return(0,U.jsxs)(xN,', 'DN=e=>{return null;let{t}=n();return(0,U.jsxs)(xN,'),
('cA=({notification:e})=>{let t=oA[e.type]', 'cA=({notification:e})=>{if(e.type===Zk.Notification_NotificationType.NOTIFICATION_TYPE_PROMOTION)return null;let t=oA[e.type]'),
('children:(0,H.jsxs)(cu,{children:[(0,H.jsxs)(jl,{children:[', 'children:(0,H.jsxs)(cu,{style:{gridTemplateColumns:`minmax(0, 1fr) minmax(240px, 28%)`,alignItems:`center`,gap:`24px`,maxWidth:`1200px`,marginInline:`auto`},children:[(0,H.jsxs)(jl,{style:{gridColumn:`auto`,height:`auto`,marginRight:0},children:['),
('(0,H.jsxs)(I,{css:{"&&::-webkit-scrollbar":{display:`none`}},display:`flex`,flexDirection:`column`,flexShrink:0,gridColumn:`span 2`,height:`100%`,minHeight:`0`,overflowY:O===`list`?void 0:`auto`,pb:n&&!w.flushAd?`4`:void 0,ref:a,width:`400px`,children:[', '(0,H.jsxs)(I,{style:{gridColumn:`auto`,height:`min(400px, calc(100vh - 140px))`,width:`100%`},css:{"&&::-webkit-scrollbar":{display:`none`}},display:`flex`,flexDirection:`column`,flexShrink:0,gridColumn:`span 2`,height:`100%`,minHeight:`0`,overflowY:O===`list`?void 0:`auto`,pb:n&&!w.flushAd?`4`:void 0,ref:a,width:`400px`,children:['),
('QW,{full:n===!1,mode:O,rootRef:D}', 'QW,{full:!1,mode:O,rootRef:D}'),
]
assert len(clean) == len(edits) + 1
for op,(find,replacement) in zip(clean[1:],edits):
    op.update(find=find,replacement=replacement)

guard = patches['local-cosmetics']['operations'][-1]
guard.update(find='Qve=async e=>{let t=await Tx(e.location);if(!t)', replacement='Qve=async e=>{let t=await Tx(e.location);if((0,S.basename)(e.location)===`lunar.jar`){if(e.expectedHash===`3a30454c0e076f6feee2246c5969d006414f4307`&&t===`83127fc392298424f53eef71f6bdb8bb61a7a2b6`)return{reason:Sx.Matched};throw Error(`Local cosmetics patch: game file or version changed. Restore the patch before updating.`)}if(!t)')
updater = patches['disable-auto-updates']['operations'][1]
updater.update(find='O9.autoUpdater.autoDownload=!1,O9.autoUpdater.autoRunAppAfterInstall=!0', replacement='O9.autoUpdater.autoDownload=!1,O9.autoUpdater.autoInstallOnAppQuit=!1,O9.autoUpdater.autoRunAppAfterInstall=!0')
startup = patches['bootstrap-download-fallback']['operations'][-1]
startup.update(find='if(t===e.expectedHash)return{reason:Sx.Matched};', replacement='if((0,S.basename)(e.location)===`genesis-0.1.0-SNAPSHOT-all.jar`){if(e.expectedHash===`ea2f19ae5fcb3941df77637b184a1ff02157d6b1`&&t===`12ec07a090a9d071363d1884a958682cd00c7d91`)return{reason:Sx.Matched};throw Error(`Bootstrap download patch: bootstrap file or version changed. Restore the patch before updating.`)}if(t===e.expectedHash)return{reason:Sx.Matched};')
for p in pack['patches']:
    for op in p['operations']:
        if op['kind'] == 'asarTextReplace':
            data = entry(op['entry'])
            assert data.decode('utf-8').count(op['find']) == 1, (p['id'], op['find'])
            op.update(sha256=sha(asar), entrySha256=sha(data))
patches['clean-launcher']['description'] = patches['clean-launcher']['description'].replace('Both packs tested on copies; launchpad and Mission Control checked live on 3.7.24-ow.', '3.7.29 compatibility is based on inspected launcher code and isolated tests; visual coverage is not yet verified.')
out = args.output
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps(pack, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
print('Built', out, 'SHA256', sha(out.read_bytes()))
