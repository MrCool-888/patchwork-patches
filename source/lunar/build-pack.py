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
parser.add_argument('--game-sha1',default='bd3d081b9c8556a1b0c98c156bc0c1b783ec5bad')
parser.add_argument('--output-dir',type=Path,default=Path('outputs'))
args=parser.parse_args()
root = Path(__file__).parent if (Path(__file__).parent/'patch_lunar.py').exists() else Path('outputs/LunarLocalPatch')
spec = importlib.util.spec_from_file_location('lunar', root/'patch_lunar.py')
lunar = importlib.util.module_from_spec(spec); spec.loader.exec_module(lunar)
game = args.original_dir/'lunar.jar'
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
clean_ops=[fuse]; cosmetic_ops=list(jar_ops)
for old,new in pairs:
    names=[name for name,data in entries.items() if data.decode('utf-8').count(old)==1]
    assert len(names)==1,(old,names)
    name=names[0]
    op=dict(kind='asarTextReplace',file=asar_file,sha256=lunar.sha(asar_data),entry=name,entrySha256=lunar.sha(entries[name]),find=old,replacement=new,count=1)
    (cosmetic_ops if old.startswith(('Jme=','SS=async')) else clean_ops).append(op)
pack=dict(schemaVersion=1,id='lunar-local-'+args.version.replace('.','-'),appId='lunar-client',appName='Lunar Client',appVersion=args.version+' / multiver 29a43e6',packVersion='0.1.1',minimumPatcherVersion='0.9.1',versionFile=asar_file,versionSha256=lunar.sha(asar_data),author='MrCool-888',source='https://github.com/MrCool-888/patchwork-patches',patches=[
    dict(id='clean-launcher',name='Remove ads and promotions',category='Launcher',version='0.1.1',status='ready',description='Experimental: hides shared launcher/status-window ads, news, featured servers, carousel, marketing links, radio upsells and promotional inbox rows. Clears layout alerts including service announcements. Disables one Electron integrity fuse; this invalidates the executable signature and removes embedded ASAR validation. Both packs tested on copies; launchpad and Mission Control checked live on 3.7.24-ow. Full UI coverage remains unverified.',operations=clean_ops),
    dict(id='local-cosmetics',name='Local cosmetics',category='Game',version='0.1.1',status='ready',dependencies=['clean-launcher'],description='Experimental: handles outfit and badge mutations locally and suppresses the inspected outgoing cosmetic push. Requires default account outfit/catalog assets. Minecraft 1.21.11 launch, locker rendering and local equip/unequip verified with launcher 3.7.24-ow; 3.7.17 tested on copies. Open Locker using the main menu shirt icon, wait for the catalog, select Local Outfit and Equip. Unequip existing online cosmetics BEFORE applying; saved online outfits can remain visible to others. Login/assets use the network; emotes/sprays are outside scope. Favorites are not persisted. Restore before game updates. Multiplayer privacy remains unverified.',operations=cosmetic_ops)
])
args.output_dir.mkdir(parents=True,exist_ok=True)
out=args.output_dir/('LunarClient-'+args.version+'.patchwork.json'); out.write_text(json.dumps(pack,indent=2)+'\n',encoding='utf-8',newline='\n')
print(json.dumps({'file':str(out),'size':out.stat().st_size,'gameEntryEditCounts':[len(x.get('edits',[])) for x in jar_ops],'launcherEdits':len(clean_ops)-1,'gameSHA1':game_sha1}))
