// Bounded read-only batch observation. No hooks, native calls or game-data writes.
const m=Process.getModuleByName('granblue_fantasy_relink.exe');
if(m.size!==136413184||Process.arch!=='x64'||Process.platform!=='windows')throw Error('target mismatch');
const types={'0x6176de0':'Em7200','0x6178120':'Em7210','0x6175aa0':'Em7201','0x6177780':'Em7211','0x59e9398':'Em7500','0x5b09148':'Em3100','0x5b77238':'Em1900','0x5b692d8':'Em1500','0x5a9d688':'Em7001','0x5b24f58':'Em7600','0x5be1e40':'Em7101'};
function countBytes(n,stride,max){if(!Number.isSafeInteger(n)||n<0||n%stride!==0||n>max*stride)throw Error('vector bounds');return n/stride;}
function vector(b,e,stride,max){const n=countBytes(Number(e.sub(b).toString()),stride,max);if(n&&b.isNull())throw Error('null vector');return n;}
if(countBytes(0,4,64)!==0||countBytes(12,4,64)!==3)throw Error('vector check');
for(const n of [-1,5,260]){let rejected=false;try{countBytes(n,4,64);}catch(e){rejected=true;}if(!rejected)throw Error('bad span accepted');}
function registryComponent(actor){
 const guard=m.base.add(0x7c28684),epoch=guard.readS32(),key=m.base.add(0x7c28680).readU32(),registry=actor.add(0xc0);
 if(epoch===0||epoch===-1||key>65535||registry.add(8).readU32()!==0)return null;
 for(const offset of [0x10,0x20]){
  const head=registry.add(offset).readPointer();if(head.isNull())continue;
  let node=head.add(8).readPointer(),best=head;const seen=new Set();
  while(!node.isNull()&&node.add(0x19).readU8()===0){
   if(seen.has(node.toString())||seen.size>=128)throw Error('component tree bounds');seen.add(node.toString());
   if(node.add(0x20).readU32()>=key){best=node;node=node.readPointer();}else node=node.add(0x10).readPointer();
  }
  if(!best.equals(head)&&best.add(0x20).readU32()===key){
   const value=best.add(0x28).readPointer();
   if(!registry.add(offset).readPointer().equals(head)||registry.add(8).readU32()!==0||m.base.add(0x7c28680).readU32()!==key||guard.readS32()!==epoch)return null;
   return value;
  }
 }
 return null;
}
function blackboardRows(actor,type){
 const component=registryComponent(actor);if(component===null||component.isNull())return {available:false};
 if(type==='Em7600'&&!component.equals(actor.add(0xbb0)))return {available:false,reason:'unexpected Em7600 embedded state component'};
 function rows(offset,kind){
  const head=component.add(offset).readPointer();if(head.isNull())return [];
  let node=head.readPointer();const result=[],seen=new Set();
  while(!node.equals(head)){
   if(node.isNull()||seen.has(node.toString())||seen.size>=512)throw Error('blackboard list bounds');seen.add(node.toString());
   const value=kind==='float'?node.add(0x14).readFloat():kind==='bool'?node.add(0x14).readU8():node.add(0x14).readS32();if(!Number.isFinite(value))throw Error('nonfinite blackboard value');
   result.push({key:node.add(0x10).readU32(),value});node=node.readPointer();
  }
  if(!component.add(offset).readPointer().equals(head))throw Error('blackboard head changed');
  return result.sort((a,b)=>a.key-b.key);
 }
 return {available:true,component:component.toString(),integers:rows(0x98,'int'),booleans:rows(0xd8,'bool'),floats:type==='Em7600'?rows(0x58,'float'):null,semantics:'Raw typed entries; only separately established keys have physical meaning'};
}
function nativeBlackboardOwner(actor,target){
 const owner=actor.add(0x648).readPointer();if(owner.isNull())return null;
 const object=owner.readPointer();if(object.isNull())return null;
 const context=object.add(0x28).readPointer();if(context.isNull())return null;
 const scene=context.add(0x18).readPointer();if(scene.isNull()||!scene.equals(target)||!scene.add(0x70).readPointer().equals(actor))return null;
 if(!actor.add(0x648).readPointer().equals(owner)||!owner.readPointer().equals(object)||!object.add(0x28).readPointer().equals(context)||!context.add(0x18).readPointer().equals(scene))return null;
 return {owner:owner.toString(),object:object.toString(),context:context.toString(),scene:scene.toString(),actor:actor.toString()};
}
function versaContext(actor,target){
 if(!actor.readPointer().equals(m.base.add(0x5b24f58)))return null;
 // Exact Em7600 ctor anchors ExEmBreakPartsInfo; damage receiver anchors the stamped proxy handle.
 const c=actor.add(0xcc8),cv=c.readPointer();let bonus={available:false};
 if(cv.equals(m.base.add(0x5b26238))){
  const owner=c.add(8).readPointer(),obj=owner.isNull()?null:owner.readPointer(),context=obj===null||obj.isNull()?null:obj.add(0x28).readPointer();
  if(context!==null&&!context.isNull()&&context.add(0x18).readPointer().equals(target)&&target.add(0x70).readPointer().equals(actor)){
   const b=c.add(0x10).readPointer(),e=c.add(0x18).readPointer(),n=vector(b,e,4,64),history=Array.from({length:n},(_,i)=>b.add(i*4).readS32());
   if(c.readPointer().equals(cv)&&c.add(8).readPointer().equals(owner)&&owner.readPointer().equals(obj)&&obj.add(0x28).readPointer().equals(context)&&context.add(0x18).readPointer().equals(target)&&c.add(0x10).readPointer().equals(b)&&c.add(0x18).readPointer().equals(e))bonus={available:true,history};
  }
 }
 const id=actor.add(0x2380).readU32();let damageProxy={present:id!==0,valid:false};
 if(id>0&&id<=1048576){
  const scene=actor.add(0x2388).readPointer(),stamp=actor.add(0x2390).readU64().toString(),manager=m.base.add(0x7020988).readPointer();
  if(!scene.isNull()&&stamp!=='0'&&!manager.isNull()){
   const slots=manager.add(0x20).readPointer(),stamps=manager.add(0x48).readPointer();
   if(!slots.isNull()&&!stamps.isNull()&&slots.add((id-1)*8).readPointer().equals(scene)&&stamps.add((id-1)*8).readU64().toString()===stamp&&scene.add(0x38).readU32()===id&&scene.add(0x40).readPointer().equals(scene)&&scene.add(0x48).readU64().toString()===stamp){
    const child=scene.add(0x70).readPointer();
    if(!child.isNull())damageProxy={present:true,valid:true,id,scene:scene.toString(),stamp,actor:child.toString(),vtableRva:child.readPointer().sub(m.base).toString()};
   }
  }
 }
 return {bonus,damageProxy,semantics:'Raw processed reward history and source-validated proxy identity; no part-health or lock mapping inferred'};
}
function readParts(actor,type,parameter,target){
 if(type==='Em3100'){
  const hb=actor.add(0xcd8).readPointer(),he=actor.add(0xce0).readPointer(),n=vector(hb,he,4,7);
  const rows=Array.from({length:7},(_,i)=>({index:i,current:actor.add(0x25b4+i*4).readS32(),initial:actor.add(0x25d0+i*4).readS32()}));
  return {rows,history:Array.from({length:n},(_,i)=>hb.add(i*4).readS32()),plausible:rows.every(x=>x.current>=0&&x.current<=1e9&&x.initial>=0&&x.initial<=1e9)};
 }
 if(type==='Em1900'){
  const rows=Array.from({length:14},(_,i)=>{const p=actor.add(0x6a68+i*0x28);return {index:i,current:p.readS32(),initial:p.add(4).readS32(),active:p.add(8).readU8(),attachment:p.add(12).readS32(),kind:p.add(16).readS32()};});
  return {rows,legDamagePhaseRaw:actor.add(0x9410).readS32(),headBreakMarkerRaw:actor.add(0x9996).readU8(),specialCurrent:actor.add(0x9420).readS32(),specialConfigured:parameter.add(0xbf0).readS32(),plausible:true,semantics:'Raw native records; selection-to-record unresolved'};
 }
 if(type==='Em1500'||type==='Em7001'||type==='Em7600'){
  const owner=type==='Em7001'?nativeBlackboardOwner(actor,target):null;
  let board=type!=='Em7001'||owner!==null?blackboardRows(actor,type):{available:false,reason:'native owner unavailable'};
  const ownerValid=type!=='Em7001'||owner!==null&&JSON.stringify(nativeBlackboardOwner(actor,target))===JSON.stringify(owner);
  if(!ownerValid)board={available:false,reason:'native owner unavailable or changed'};
  let processedHistory=null;
  if(type==='Em7001'&&ownerValid&&board.available){const hb=actor.add(0xcd8).readPointer(),he=actor.add(0xce0).readPointer(),n=vector(hb,he,4,64);processedHistory=Array.from({length:n},(_,i)=>hb.add(i*4).readS32());}
  return {blackboard:board,processedHistory,nativeOwner:ownerValid?owner:null,configuredHead:type==='Em7001'&&ownerValid&&board.available?parameter.add(0x5978).readS32():null,
   versaContext:type==='Em7600'?versaContext(actor,target):null,golemBehaviorCounter:type==='Em1500'?{phase:actor.add(0x2898).readS32(),value:actor.add(0x289c).readS32()}:null,
   plausible:type!=='Em7001'||ownerValid&&board.available,semantics:'Known behavior counter is not physical-part HP; no UI activation'};
 }
 if(type==='Em7101'){
  const c=actor.add(0x1a78),ownerId=c.add(8).readU32(),ownerScene=c.add(0x10).readPointer(),ownerStamp=c.add(0x18).readU64().toString();
  const b=c.add(0x20).readPointer(),e=c.add(0x28).readPointer(),n=vector(b,e,24,8),definitionOwner=c.add(0x58).readPointer();
  const manager=m.base.add(0x7020988).readPointer(),slots=manager.add(0x20).readPointer(),stamps=manager.add(0x48).readPointer();
  const rows=Array.from({length:n},(_,i)=>{const p=b.add(i*24),id=p.readU32(),scene=p.add(8).readPointer(),stamp=p.add(16).readU64().toString();
   if(id<1||id>1048576||scene.isNull()||stamp==='0'||!slots.add((id-1)*8).readPointer().equals(scene)||stamps.add((id-1)*8).readU64().toString()!==stamp)return {id,valid:false};
   if(scene.add(0x38).readU32()!==id||!scene.add(0x40).readPointer().equals(scene)||scene.add(0x48).readU64().toString()!==stamp)return {id,valid:false};
   const child=scene.add(0x70).readPointer();if(child.isNull())return {id,valid:false};
   const vtableRva=child.readPointer().sub(m.base).toString();if(vtableRva!=='0x5d825e0')return {id,vtableRva,valid:false};
   const definition=child.add(0x5e0).readS32(),current=child.add(0x5c4).readFloat(),initial=child.add(0x5c0).readFloat();
   const valid=definition===0&&child.add(0x368).readU32()===ownerId&&child.add(0x370).readPointer().equals(ownerScene)&&child.add(0x378).readU64().toString()===ownerStamp&&Number.isFinite(current)&&Number.isFinite(initial)&&initial>=1&&initial<=1e9&&current>=0&&current<=initial;
   return {id,scene:scene.toString(),stamp,child:child.toString(),vtableRva,definition,current,initial,valid};});
  return {ownerId,ownerScene:ownerScene.toString(),ownerStamp,definitionOffset:definitionOwner.sub(parameter).toString(),rows,plausible:ownerScene.equals(target)&&ownerId===target.add(0x38).readU32()&&ownerStamp===target.add(0x48).readU64().toString()&&n===1&&rows.every(x=>x.valid)};
 }

 const human=type==='Em7201'||type==='Em7211',legs=type==='Em7200'||type==='Em7210';
 if(human||legs){
  const count=human?7:9,stride=human?12:20,start=human?0x2604:0x25e8;
  const rows=Array.from({length:count},(_,i)=>{const p=actor.add(start+i*stride);return {index:i,current:p.readS32(),initial:p.add(4).readS32(),broken:p.add(8).readU8()};});
  const maps=legs?Array.from({length:9},(_,i)=>actor.add(0x25bc+i*4).readS32()):null;
  const configured=human?Array.from({length:5},(_,i)=>parameter.add(0x8b8+i*4).readS32()):null;
  return {rows,maps,configured,plausible:rows.every(x=>x.current>=0&&x.current<=1e9&&x.initial>=0&&x.initial<=1e9&&x.broken<=1)};
 }
 if(type!=='Em7500')throw Error('unmapped profile');
 const c=actor.add(0x21b0),cv=c.readPointer(),owner=c.add(8).readPointer(),obj=owner.readPointer(),context=obj.add(0x28).readPointer();
 if(cv.isNull()||!context.add(0x18).readPointer().equals(target))throw Error('controller owner mismatch');
 const sb=c.add(0x10).readPointer(),se=c.add(0x18).readPointer(),db=c.add(0x28).readPointer(),de=c.add(0x30).readPointer(),n=vector(sb,se,12,64);
 if(n===0||vector(db,de,56,64)!==n)throw Error('controller vector mismatch');
 const rows=Array.from({length:n},(_,i)=>{const p=sb.add(i*12),d=db.add(i*56);return {index:i,part:d.add(4).readS32(),eligible:d.add(8).readU8(),current:p.readS32(),initial:p.add(4).readS32(),broken:p.add(8).readU8()};});
 return {mode:actor.add(0x2288).readS32(),controllerVtable:cv.toString(),owner:owner.toString(),ownerObject:obj.toString(),ownerContext:context.toString(),states:sb.toString(),definitions:db.toString(),rows,plausible:rows.every(x=>x.current>=0&&x.current<=1e9&&x.initial>=0&&x.initial<=1e9&&x.broken<=1&&x.eligible<=1)};
}
let lastRoot=null;
function readRootReference(saved,worldKey,matches){
 if(saved===null||saved.worldKey!==worldKey)return null;
 function valid(){return matches(saved.scene,saved.actor,saved.id,saved.stamp)&&saved.actor.readPointer().equals(saved.vtable)&&types[saved.vtable.sub(m.base).toString()]===saved.type&&saved.actor.add(0x1598).readPointer().equals(saved.parameter);}
 if(!valid())return null;
 const profile=readParts(saved.actor,saved.type,saved.parameter,saved.scene);if(!valid()||JSON.stringify(readParts(saved.actor,saved.type,saved.parameter,saved.scene))!==JSON.stringify(profile))return null;
 return {type:saved.type,actor:saved.actor.toString(),targetHandle:{id:saved.id,stamp:saved.stamp},profile,relationship:'Previously observed exact root, revalidated in this world; equipment ownership is not established'};
}
function sample(){
 const g=m.base.add(0x7035ca0),id=g.readU32(),scene=g.add(8).readPointer(),stamp=g.add(16).readU64().toString(),direct=m.base.add(0x7035d00).readPointer(),session=m.base.add(0x7c53d10).readPointer();
 if(session.isNull()||session.add(4).readS32()!==0)return {valid:false,reason:'offline unavailable'};
 if(id<1||id>1048576||scene.isNull()||direct.isNull()||stamp==='0')return {valid:false,reason:'local handle unavailable'};
 const manager=m.base.add(0x7020988).readPointer();if(manager.isNull())return {valid:false,reason:'scene manager unavailable'};
 const slots=manager.add(0x20).readPointer(),stamps=manager.add(0x48).readPointer();
 function matches(s,a,i,t){return i>0&&i<=1048576&&t!=='0'&&!s.isNull()&&slots.add((i-1)*8).readPointer().equals(s)&&stamps.add((i-1)*8).readU64().toString()===t&&s.add(0x38).readU32()===i&&s.add(0x40).readPointer().equals(s)&&s.add(0x48).readU64().toString()===t&&s.add(0x70).readPointer().equals(a);}
 if(!matches(scene,direct,id,stamp))return {valid:false,reason:'local scene mismatch'};
 const rb=m.base.add(0x7362690).readPointer(),re=m.base.add(0x7362698).readPointer(),n=vector(rb,re,8,16),owners=[];
 for(let i=0;i<n;i++){const ex=rb.add(i*8).readPointer();if(ex.isNull()||!ex.add(0x230).readPointer().equals(direct))continue;const a=ex.add(8).readPointer();if(a.isNull())continue;const b=a.readPointer();if(b.isNull())continue;const c=b.add(0x28).readPointer();if(!c.isNull()&&c.add(0x18).readPointer().equals(scene))owners.push({ex,entry:rb.add(i*8),owner:c});}
 if(owners.length!==1)return {valid:false,reason:'unique controlled owner unavailable'};
 const {ex,entry,owner}=owners[0],target=ex.add(0x108).readPointer(),selected=ex.add(0x118).readS32();
 const worldStable=()=>matches(scene,direct,id,stamp)&&entry.readPointer().equals(ex)&&owner.add(0x18).readPointer().equals(scene)&&ex.add(0x230).readPointer().equals(direct)&&m.base.add(0x7362690).readPointer().equals(rb)&&m.base.add(0x7362698).readPointer().equals(re)&&g.readU32()===id&&g.add(8).readPointer().equals(scene)&&g.add(16).readU64().toString()===stamp&&m.base.add(0x7035d00).readPointer().equals(direct)&&m.base.add(0x7c53d10).readPointer().equals(session)&&session.add(4).readS32()===0&&m.base.add(0x7020988).readPointer().equals(manager)&&manager.add(0x20).readPointer().equals(slots)&&manager.add(0x48).readPointer().equals(stamps);
 const worldKey=[id,scene.toString(),stamp,direct.toString(),ex.toString()].join('/');
 if(lastRoot!==null&&lastRoot.worldKey!==worldKey)lastRoot=null;
 if(target.isNull()){
  const reference=worldStable()?readRootReference(lastRoot,worldKey,matches):null;
  const stable=worldStable()&&ex.add(0x108).readPointer().isNull()&&ex.add(0x118).readS32()===selected;
  return {valid:false,reason:'no lock target',selected,profile:{referenceRoot:stable?reference:null,semantics:'Separate exact root during lock loss; no current target or equipment ownership inferred'}};
 }
 const tid=target.add(0x38).readU32(),ts=target.add(0x48).readU64().toString();
 if(tid<1||tid>1048576||ts==='0'||!slots.add((tid-1)*8).readPointer().equals(target)||stamps.add((tid-1)*8).readU64().toString()!==ts)return {valid:false,reason:'stale target handle'};
 const actor=target.add(0x70).readPointer();if(actor.isNull()||!matches(target,actor,tid,ts))return {valid:false,reason:'target scene mismatch'};
 const vt=actor.readPointer(),vr=vt.sub(m.base).toString(),type=types[vr],mapped=type!==undefined;
 const parameter=mapped?actor.add(0x1598).readPointer():null;if(mapped&&parameter.isNull())return {valid:false,reason:'missing parameter'};
 const readProfile=()=>mapped?readParts(actor,type,parameter,target):{plausible:false,referenceRoot:readRootReference(lastRoot,worldKey,matches),semantics:'Unmapped current target; no equipment-to-root relationship inferred'};
 const profile=readProfile(),profileSignature=JSON.stringify(profile);
 const lb=mapped?actor.add(0x788).readPointer():null,le=mapped?actor.add(0x790).readPointer():null,ln=mapped?vector(lb,le,48,512):0,locks=Array.from({length:ln},(_,i)=>{const p=lb.add(i*48);return {index:i,id:p.readS32(),flags28:p.add(0x28).readU8(),enabled29:p.add(0x29).readU8()};});
 const stable=matches(scene,direct,id,stamp)&&matches(target,actor,tid,ts)&&actor.readPointer().equals(vt)&&(!mapped||actor.add(0x1598).readPointer().equals(parameter))&&JSON.stringify(readProfile())===profileSignature&&(!mapped||actor.add(0x788).readPointer().equals(lb)&&actor.add(0x790).readPointer().equals(le))&&locks.every(x=>{const p=lb.add(x.index*48);return p.readS32()===x.id&&p.add(0x28).readU8()===x.flags28&&p.add(0x29).readU8()===x.enabled29;})&&ex.add(0x108).readPointer().equals(target)&&ex.add(0x118).readS32()===selected&&worldStable();
 if(stable&&mapped)lastRoot={worldKey,scene:target,actor,id:tid,stamp:ts,parameter,type,vtable:vt};
 return {valid:stable&&mapped&&profile.plausible,identityValid:stable,profileMapped:mapped,type:type||'Unmapped',vtableRva:vr,actor:actor.toString(),targetHandle:{id:tid,stamp:ts},selected,bodyHp:mapped?actor.add(0x160).readU32():null,flags:mapped?actor.add(0xe).readU8():null,profile,locks};
}
let timer=null,closed=false,ticks=0,events=0,last='';const start=Date.now();
function finish(reason){if(closed)return;closed=true;if(timer!==null)clearInterval(timer);send({kind:'compressed-batch-watch-end',reason,ticks,events,elapsedMs:Date.now()-start,hooks:0,targetCalls:0,gameDataWrites:0});}
function tick(){
 if(closed)return;ticks++;
 try{const s=sample();
  const sig=JSON.stringify(s);if(sig!==last||ticks%20===0){send({kind:'compressed-batch-watch-sample',elapsedMs:Date.now()-start,...s});last=sig;events++;}
 }catch(e){send({kind:'compressed-batch-watch-error',error:String(e)});finish('read exception');}
 if(events>=1800)finish('event limit');if(Date.now()-start>=900000)finish('time limit');
}
rpc.exports={stop(){finish('caller stop');return {closed,ticks,events};}};
send({kind:'compressed-batch-watch-ready',pid:Process.id,maxDurationMs:900000,intervalMs:250,hooks:0,targetCalls:0,gameDataWrites:0});tick();if(!closed)timer=setInterval(tick,250);
