import base64
import argparse
import hashlib
import importlib.util
import json
import struct
import zipfile
from pathlib import Path

parser=argparse.ArgumentParser(description='Build the declarative Lunar Patchwork pack from exact local originals.')
parser.add_argument('--version', choices=['3.7.17-ow','3.7.24-ow'],default='3.7.17-ow')
parser.add_argument('--original-dir',type=Path,default=Path('work/patch-test/backup'))
parser.add_argument('--game-sha1',default='83127fc392298424f53eef71f6bdb8bb61a7a2b6')
parser.add_argument('--bootstrap-sha1',default='12ec07a090a9d071363d1884a958682cd00c7d91')
parser.add_argument('--output-dir',type=Path,default=Path('outputs'))
args=parser.parse_args()
root = Path(__file__).parent if (Path(__file__).parent/'patch_lunar.py').exists() else Path('outputs/LunarLocalPatch')
spec = importlib.util.spec_from_file_location('lunar', root/'patch_lunar.py')
lunar = importlib.util.module_from_spec(spec); spec.loader.exec_module(lunar)
game = args.original_dir/'lunar.jar'
genesis = args.original_dir/'genesis-0.1.0-SNAPSHOT-all.jar'
exe_data = (args.original_dir/'Lunar Client.exe').read_bytes()
asar_data = (args.original_dir/'app.asar').read_bytes()
jar_hash = hashlib.sha256(game.read_bytes()).hexdigest()
assert jar_hash == lunar.EXPECTED['lunar.jar']
expected={
 '3.7.17-ow':('cc33e5054e13a68b980d1496116c74e7b89d38cbcd18b6ea27c3d392609065c4','b0493c048544f465de9867d5d8cbf2dd80da75a9b4635a567b397b80741d8acc'),
 '3.7.24-ow':('506574f17a6b7aeb5a13fe5f53fc83329fa705141505dde30ec88e00aa03a9f4','e21d5ad61a3caec8e25948a52f3a0157b9e875ef6cd9a47cd1ee874a7e58f5e6')
}
assert (lunar.sha(asar_data),lunar.sha(exe_data))==expected[args.version]
exe_file = 'AppData/Local/Programs/Lunar Client/Lunar Client.exe'
asar_file = 'AppData/Local/Programs/Lunar Client/resources/app.asar'
jar_file = '.lunarclient/offline/multiver/lunar.jar'
b64 = lambda x: base64.b64encode(x).decode('ascii')
def splice(kind, file, original, changed, file_hash=None, entry=None):
    edits = class_edits.pop(lunar.sha(original))
    result = bytearray(original)
    for e in reversed(edits): result[e['offset']:e['offset']+e['length']] = base64.b64decode(e['dataBase64'])
    assert result == changed and len(edits) <= 256
    op = dict(kind=kind,file=file,sha256=file_hash or lunar.sha(original),resultSha256=lunar.sha(changed),edits=edits)
    if entry: op.update(entry=entry,entrySha256=lunar.sha(original))
    return op
class_edits = {}
real_methods = lunar.ClassFile.replace_methods
def capture_methods(cf,bodies):
    # Preserve the original pool and member bytes; publish only added constants/new Code attributes.
    old = cf.original
    pool_count = struct.unpack_from('>H',old,8)[0]
    tail_start = len(old)-len(cf.tail)
    edits=[]
    if len(cf.cp)!=pool_count:
        edits.append(dict(offset=8,length=2,dataBase64=b64(struct.pack('>H',len(cf.cp)))))
        edits.append(dict(offset=tail_start,length=0,dataBase64=b64(b''.join(x for x in cf.cp[pool_count:] if x))))
    for kind,name,descriptor,attrs in cf.members:
        if kind!='method' or (name,descriptor) not in bodies: continue
        code,stack,locals_count=bodies[(name,descriptor)]
        for start,end,attr,index in attrs:
            if attr!='Code': continue
            payload=struct.pack('>HHI',stack,locals_count,len(code))+code+b'\0\0\0\0'
            edits.append(dict(offset=tail_start+start,length=end-start,dataBase64=b64(struct.pack('>HI',index,len(payload))+payload)))
    class_edits[lunar.sha(old)]=sorted(edits,key=lambda x:x['offset'])
    return real_methods(cf,bodies)
real_init=lunar.ClassFile.__init__
def capture_init(cf,data):
    real_init(cf,data); cf.original=data
lunar.ClassFile.__init__=capture_init
lunar.ClassFile.replace_methods=capture_methods
# Only a single fuse byte changes; no executable code or original executable is embedded.
sentinel=b'dL7pKGdnNz796PbbjQWNKmHXBZaB9tsX'
offset=exe_data.index(sentinel)+len(sentinel)+2+4
wire_start=offset-6
assert exe_data.count(sentinel)==1 and exe_data[wire_start:wire_start+2]==(b'\x01\x08' if args.version=='3.7.17-ow' else b'\x01\x09')
assert exe_data[wire_start+2:wire_start+10]==b'10111101'
new_exe=bytearray(exe_data);new_exe[offset]=ord('0');new_exe=bytes(new_exe)
assert exe_data[:offset]==new_exe[:offset] and exe_data[offset+1:]==new_exe[offset+1:]
fuse = dict(kind='binarySplice',file=exe_file,sha256=lunar.sha(exe_data),resultSha256=lunar.sha(new_exe),edits=[dict(offset=offset,length=1,dataBase64=b64(new_exe[offset:offset+1]))])
jar_ops = []
with zipfile.ZipFile(game) as z:
    stub = 'com/lunarclient/websocket/cosmetic/v2/CosmeticService$Stub.class'
    badge = 'com/lunarclient/websocket/badge/v1/BadgeService$Stub.class'
    for name,badge_mode in [(stub,False),(badge,True)]:
        old = z.read(name); new = lunar.patch_stub(old,badge_mode)
        jar_ops.append(splice('jarEntrySplice',jar_file,old,new,jar_hash,name))
    old = z.read(lunar.COSMETIC_MANAGER)
    new = lunar.ClassFile(old).replace_methods({(lunar.COSMETIC_PUSH_METHOD,'()V'):(b'\xb1',0,1)})
    jar_ops.append(splice('jarEntrySplice',jar_file,old,new,jar_hash,lunar.COSMETIC_MANAGER))
    old = z.read(lunar.CLOTH_MENU)
    new, edits = lunar.patch_cloth_menu(old)
    class_edits[lunar.sha(old)] = [dict(offset=o,length=n,dataBase64=b64(v)) for o,n,v in edits]
    jar_ops.append(splice('jarEntrySplice',jar_file,old,new,jar_hash,lunar.CLOTH_MENU))
    for name in ['LocalCosmetics.class','LocalCosmetics$1.class']:
        data = (root/'helper'/name).read_bytes()
        jar_ops.append(dict(kind='jarEntryAdd',file=jar_file,sha256=jar_hash,entry='local/lunar/'+name,entrySha256=lunar.sha(data),dataBase64=b64(data)))

_, members, base = lunar.asar_read(asar_data)
entries = {}
for name in ['dist-electron/electron/main.js','dist/assets/use-show-ad.js','dist/assets/main.js','dist/assets/topbar.js']:
    m=members[name]; start=base+int(m['offset']); entries[name]=asar_data[start:start+m['size']]
pairs=[]
real_replace=lunar.replace_once
def capture(text,old,new):
    pairs.append((old,new)); return real_replace(text,old,new)
lunar.replace_once=capture
game_sha1=args.game_sha1
assert len(game_sha1)==40 and all(c in '0123456789abcdef' for c in game_sha1)
if args.version=='3.7.17-ow':
    lunar.patch_asar(asar_data,game_sha1)
else:
    layout='return{alert:aWe(e.alert),blogPosts:oWe(e.blogPosts,W9.getReadPosts()),serversCta:e.serversCta,servers:e.shelfServers,navItems:e.navItems,videoPromotion:e.videoPromotion,ads:e.ads,theme:`default`,carousel:e.carousel,announcements:e.announcements}'
    guard='SS=async e=>{let t=await bS(e.location);if((0,S.basename)(e.location)===`lunar.jar`){if(e.expectedHash===`'+lunar.ORIGINAL_GAME_SHA1+'`&&t===`'+game_sha1+'`)return{reason:yS.Matched};throw Error(`Local cosmetics patch: game file or version changed. Restore the patch before updating.`)}if(!t)'
    pairs=[
      (layout,'return{alert:void 0,blogPosts:[],serversCta:void 0,servers:[],navItems:[],videoPromotion:void 0,ads:[],theme:`default`,carousel:[],announcements:[]}'),
      ('SS=async e=>{let t=await bS(e.location);if(!t)',guard),
      ('fn=e=>{let t=j(e=>e.metadata.ads)','fn=e=>{return false;let t=j(e=>e.metadata.ads)'),
      ('un=e=>{let t=Bt()','un=e=>{return null;let t=Bt()'),
      ('onHighImpactRemoved:c})=>{let l=(0,R.useRef)','onHighImpactRemoved:c})=>{return null;let l=(0,R.useRef)'),
      ('hH=()=>{let{t:e}=M()','hH=()=>{return null;let{t:e}=M()'),
      ('fU=()=>{let{t:e}=M()','fU=()=>{return null;let{t:e}=M()'),
      ('u_=()=>{let{t:e}=M()','u_=()=>{return null;let{t:e}=M()'),
      ('videoPromotion:r,promotionEligible:i,t:e}),[e,n,r,t,i]','videoPromotion:r,promotionEligible:i,t:e}).filter(e=>e.type!==`external`&&e.type!==`promotion`),[e,n,r,t,i]'),
      ('CF=e=>{let{t}=O();return(0,H.jsxs)(_F,','CF=e=>{return null;let{t}=O();return(0,H.jsxs)(_F,'),
      ('uA=({notification:e})=>{let t=cA[e.type]','uA=({notification:e})=>{if(e.type===$k.Notification_NotificationType.NOTIFICATION_TYPE_PROMOTION)return null;let t=cA[e.type]')
    ]
# Scope the compact layout to Home; shared grid components serve other pages.
home_js, home_left, home_friends = ('B', 'su', 'UR') if args.version == '3.7.17-ow' else ('H', 'pu', 'YV')
home_root = 'P' if args.version == '3.7.17-ow' else 'L'
home_grid = 'children:(0,'+home_js+'.jsxs)(Ul,{children:[(0,'+home_js+'.jsxs)('+home_left+',{children:['
home_grid_new = 'children:(0,'+home_js+'.jsxs)(Ul,{style:{gridTemplateColumns:`minmax(0, 1fr) minmax(240px, 28%)`,alignItems:`center`,gap:`24px`,maxWidth:`1200px`,marginInline:`auto`},children:[(0,'+home_js+'.jsxs)('+home_left+',{style:{gridColumn:`auto`,height:`auto`,marginRight:0},children:['
friends_props = 'display:`flex`,flexDirection:`column`,flexShrink:0,gridColumn:`span 2`,height:`100%`,minHeight:`0`,'+('ref:a,' if args.version == '3.7.24-ow' else '')+'width:`400px`,children:['
friends_old = '(0,'+home_js+'.jsxs)('+home_root+',{'+friends_props
friends_new = '(0,'+home_js+'.jsxs)('+home_root+',{style:{gridColumn:`auto`,height:`min(400px, calc(100vh - 140px))`,width:`100%`},'+friends_props
pairs += [(home_grid,home_grid_new),(friends_old,friends_new),(home_friends+',{full:r===!1}',home_friends+',{full:!1}')]
clean_ops=[fuse]; cosmetic_ops=list(jar_ops)
updater = 'M9' if args.version == '3.7.17-ow' else 'j9'
update_pairs = [
    ('async checkForUpdates(e){if(e&&(this.userInitiated=e),',
     'async checkForUpdates(e){if(!e){this.logger.info(`Automatic launcher updates disabled by Patchwork.`);return}if(e&&(this.userInitiated=e),'),
    (updater+'.autoUpdater.autoDownload=!1,'+updater+'.autoUpdater.autoRunAppAfterInstall=!0',
     updater+'.autoUpdater.autoDownload=!1,'+updater+'.autoUpdater.autoInstallOnAppQuit=!1,'+updater+'.autoUpdater.autoRunAppAfterInstall=!0'),
    ('q9.checkForUpdates(e.forceInstall)',
     'this.client.assetServerModule.logger.info(`Server-requested launcher update blocked by Patchwork.`)')
]
update_ops=[]
for old,new in update_pairs:
    name='dist-electron/electron/main.js'
    assert entries[name].decode('utf-8').count(old)==1,old
    update_ops.append(dict(kind='asarTextReplace',file=asar_file,sha256=lunar.sha(asar_data),entry=name,entrySha256=lunar.sha(entries[name]),find=old,replacement=new,count=1))
for old,new in pairs:
    names=[name for name,data in entries.items() if data.decode('utf-8').count(old)==1]
    assert len(names)==1,(old,names)
    name=names[0]
    op=dict(kind='asarTextReplace',file=asar_file,sha256=lunar.sha(asar_data),entry=name,entrySha256=lunar.sha(entries[name]),find=old,replacement=new,count=1)
    (cosmetic_ops if old.startswith(('Jme=','SS=async')) else clean_ops).append(op)
startup_spec=importlib.util.spec_from_file_location('startup',root/'patch_startup.py')
startup=importlib.util.module_from_spec(startup_spec); startup_spec.loader.exec_module(startup)
assert lunar.sha(genesis.read_bytes())==startup.GENESIS_SHA256
with zipfile.ZipFile(genesis) as z:
    old=z.read(startup.HTTP_ENTRY)
    new,edits=startup.patch_http(old,lunar.ClassFile)
startup_ops=[dict(kind='jarEntrySplice',file='.lunarclient/offline/multiver/genesis-0.1.0-SNAPSHOT-all.jar',sha256=startup.GENESIS_SHA256,entry=startup.HTTP_ENTRY,entrySha256=lunar.sha(old),resultSha256=lunar.sha(new),edits=edits)]
for helper_name in ('BootstrapDownloads.class','BootstrapDownloads$Origin.class'):
    bootstrap_helper=(root/'helper'/'local'/'lunar'/helper_name).read_bytes()
    startup_ops.append(dict(kind='jarEntryAdd',file='.lunarclient/offline/multiver/genesis-0.1.0-SNAPSHOT-all.jar',sha256=startup.GENESIS_SHA256,entry='local/lunar/'+helper_name,entrySha256=lunar.sha(bootstrap_helper),dataBase64=b64(bootstrap_helper)))
bootstrap_sha1=args.bootstrap_sha1
assert len(bootstrap_sha1)==40 and all(c in '0123456789abcdef' for c in bootstrap_sha1)
matched='WS' if args.version=='3.7.17-ow' else 'yS'
verify_prefix='if(t===e.expectedHash)return{reason:'+matched+'.Matched};'
verify_guard='if((0,S.basename)(e.location)===`genesis-0.1.0-SNAPSHOT-all.jar`){if(e.expectedHash===`'+startup.GENESIS_SHA1+'`&&t===`'+bootstrap_sha1+'`)return{reason:'+matched+'.Matched};throw Error(`Bootstrap download patch: bootstrap file or version changed. Restore the patch before updating.`)}'+verify_prefix
name='dist-electron/electron/main.js'
assert entries[name].decode('utf-8').count(verify_prefix)==1
startup_ops.append(dict(kind='asarTextReplace',file=asar_file,sha256=lunar.sha(asar_data),entry=name,entrySha256=lunar.sha(entries[name]),find=verify_prefix,replacement=verify_guard,count=1))
promotions_spec=importlib.util.spec_from_file_location('promotions',root/'patch_promotions.py')
promotions=importlib.util.module_from_spec(promotions_spec); promotions_spec.loader.exec_module(promotions)
ui_data=(args.original_dir/'index.js').read_bytes()
promotions.patch_ui(ui_data)
promotion_ops=[dict(kind='textReplace',file=promotions.UI_FILE,sha256=promotions.UI_SHA256,find=old,replacement=new,count=1) for old,new in promotions.REPLACEMENTS]
pack=dict(schemaVersion=1,id='lunar-local-'+args.version.replace('.','-'),appId='lunar-client',appName='Lunar Client',appVersion=args.version+' / multiver 29a43e6',packVersion='0.1.6',minimumPatcherVersion='0.9.2',versionFile=asar_file,versionSha256=lunar.sha(asar_data),author='MrCool-888',source='https://github.com/MrCool-888/patchwork-patches',patches=[
    dict(id='clean-launcher',name='Remove ads and promotions',category='Launcher',version='0.1.2',status='ready',description='Experimental: hides shared launcher/status-window ads, news, featured servers, carousel, marketing links, radio upsells and promotional inbox rows. Centers the Home launch card and Friends panel in a bounded two-column layout. Clears layout alerts including service announcements. Disables one Electron integrity fuse; this invalidates the executable signature and removes embedded ASAR validation. Both packs tested on copies; launchpad and Mission Control checked live on 3.7.24-ow. Full UI coverage remains unverified.',operations=clean_ops),
    dict(id='local-cosmetics',name='Local cosmetics',category='Game',version='0.1.2',status='ready',dependencies=['clean-launcher'],description='Experimental: local outfit/badge mutations, inspected outgoing cosmetic push suppressed, and built-in cloth cloak physics available through Locker. Select Local Outfit, Equip a cloak, and enable Cloth Cloak beneath the character preview. Cloth settings save locally; Render Cloth Cloaks must be enabled in Lunar cosmetic settings. Requires account/default outfit/catalog assets. Unequip online cosmetics BEFORE applying; saved online outfits can remain visible to others. Login/assets use the network; emotes/sprays are outside scope and favorites are not persisted. Restore before game updates. Multiplayer privacy remains unverified.',operations=cosmetic_ops),
    dict(id='disable-auto-updates',name='Disable automatic updates',category='Launcher',version='0.1.0',status='ready',dependencies=['clean-launcher'],description='Stops automatic launcher update checks at startup, on timers and after channel changes. Blocks server-requested launcher updates, including force-install requests, and disables installation on app exit. Manual Check for updates remains available; restore patches before updating Lunar. Requires Remove ads and promotions for its launcher archive compatibility change. Game/runtime downloads during Minecraft launch are separate and remain unchanged.',operations=update_ops),
    dict(id='bootstrap-download-fallback',name='Faster bootstrap download fallback',category='Game',version='0.1.1',status='ready',dependencies=['clean-launcher'],description='Optional: limits connection establishment to 15 seconds and tries each existing Maven repository once. After a repository connection times out, skips that origin for subsequent Maven artifacts in the same JVM, avoiding repeated 15-second waits on cold-cache launches such as 26.3. A fresh launch retries all origins. Keeps the 180-second request deadline, reachable repository order, cached archive checks and download validation. Single-URL retry behavior remains unchanged. Supports only the exact tested Genesis fingerprint. Restore before updating Lunar. Cannot recover a dependency unavailable from every remaining repository.',operations=startup_ops),
    dict(id='clean-game-promotions',name='Remove in-game promotions',category='Game',version='0.1.0',status='ready',description='Experimental: hides main-menu Store/Discover buttons, news carousel, coin purchase control, Medal quests/rewards, promotional video and Discord reward cards, countdown, Wrapped card and partner radio widget. Removes the Locker shop/Purchase Lunar+ link and preview checkout/back-to-store controls. Keeps Singleplayer, Multiplayer, Locker/local outfits, theme selector, ordinary alerts and preview exit controls. Requires the exact inspected extracted UI bundle b6d39b2; changing UI builds or re-extracting the cache can remove the cleanup. No launcher/game/bootstrap verification bypass is added. Automated checks passed; live game testing is requested from the user.',operations=promotion_ops)
])
args.output_dir.mkdir(parents=True,exist_ok=True)
out=args.output_dir/('LunarClient-'+args.version+'.patchwork.json'); out.write_text(json.dumps(pack,indent=2)+'\n',encoding='utf-8',newline='\n')
print(json.dumps({'file':str(out),'size':out.stat().st_size,'gameEntryEditCounts':[len(x.get('edits',[])) for x in jar_ops],'launcherEdits':len(clean_ops)-1,'gameSHA1':game_sha1}))
