// Same-build evidence: em7400_child_hp_slots_20261108.json; max is raw, not an assumed initial value.
// Bounded read-only batch observation. No hooks, native calls or game-data writes.
const m=Process.getModuleByName('granblue_fantasy_relink.exe');
if(m.size!==136413184||Process.arch!=='x64'||Process.platform!=='windows')throw Error('target mismatch');
const types={'0x6163b50':'Em7401','0x6164a90':'Em7404','0x6165230':'Em7406','0x61659d0':'Em7407'};
function countBytes(n,stride,max){if(!Number.isSafeInteger(n)||n<0||n%stride!==0||n>max*stride)throw Error('vector bounds');return n/stride;}
function vector(b,e,stride,max){const n=countBytes(Number(e.sub(b).toString()),stride,max);if(n&&b.isNull())throw Error('null vector');return n;}
if(countBytes(0,4,64)!==0||countBytes(12,4,64)!==3)throw Error('vector check');
for(const n of [-1,5,260]){let rejected=false;try{countBytes(n,4,64);}catch(e){rejected=true;}if(!rejected)throw Error('bad span accepted');}
function readParts(actor,type,parameter,target){
 const tables={Em7401:[0x6163b50,0x5bf0808],Em7404:[0x6164a90,0x5bf2c68],Em7406:[0x6165230,0x5bf4108],Em7407:[0x61659d0,0x5d9eed8]},expected=tables[type];
 if(!expected||!actor.readPointer().equals(m.base.add(expected[0]))||!actor.add(0x150).readPointer().equals(m.base.add(expected[1])))return {plausible:false,reason:'exact child/ExHp table mismatch'};
 const currentRaw=actor.add(0x160).readU64().toString(),maximumRaw=actor.add(0x168).readU64().toString(),current=Number(currentRaw),maximum=Number(maximumRaw);
 const manager=m.base.add(0x7020988).readPointer();
 if(manager.isNull())return {plausible:false,reason:'parent manager unavailable'};
 const slots=manager.add(0x20).readPointer(),stamps=manager.add(0x48).readPointer(),id=actor.add(0x1a50).readU32(),scene=actor.add(0x1a58).readPointer(),stamp=actor.add(0x1a60).readU64().toString();
 if(id<1||id>1048576||scene.isNull()||stamp==='0'||slots.isNull()||stamps.isNull()||!slots.add((id-1)*8).readPointer().equals(scene)||stamps.add((id-1)*8).readU64().toString()!==stamp)return {plausible:false,reason:'stale parent handle',id};
 if(scene.add(0x38).readU32()!==id||!scene.add(0x40).readPointer().equals(scene)||scene.add(0x48).readU64().toString()!==stamp)return {plausible:false,reason:'parent self handle mismatch'};
 const parentActor=scene.add(0x70).readPointer();
 if(parentActor.isNull())return {plausible:false,reason:'parent actor unavailable'};
 const parent={id,scene:scene.toString(),stamp,actor:parentActor.toString(),vtableRva:parentActor.readPointer().sub(m.base).toString()};
 return {currentRaw,maximumRaw,current,maximum,parent,plausible:Number.isSafeInteger(current)&&Number.isSafeInteger(maximum)&&current>=0&&maximum>=1&&maximum<=1e9&&current<=maximum,semantics:'Raw typed child ExHp values and stamped parent link; no physical-break or processed-state meaning inferred'};
}
let lastChild=null;
function readChildReference(saved,worldKey,matches){
 if(saved===null||saved.worldKey!==worldKey)return null;
 function valid(){return matches(saved.scene,saved.actor,saved.id,saved.stamp)&&saved.actor.readPointer().equals(saved.vtable)&&types[saved.vtable.sub(m.base).toString()]===saved.type&&saved.actor.add(0x1598).readPointer().equals(saved.parameter);}
 if(!valid())return null;
 const profile=readParts(saved.actor,saved.type,saved.parameter,saved.scene);if(!valid()||JSON.stringify(readParts(saved.actor,saved.type,saved.parameter,saved.scene))!==JSON.stringify(profile))return null;
 return {type:saved.type,actor:saved.actor.toString(),targetHandle:{id:saved.id,stamp:saved.stamp},profile,relationship:'Previously selected exact child, revalidated in this world; physical-break meaning is not established'};
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
 if(lastChild!==null&&lastChild.worldKey!==worldKey)lastChild=null;
 if(target.isNull()){
  const reference=worldStable()?readChildReference(lastChild,worldKey,matches):null;
  const stable=worldStable()&&ex.add(0x108).readPointer().isNull()&&ex.add(0x118).readS32()===selected;
  return {valid:false,reason:'no lock target',selected,profile:{referenceChild:stable?reference:null,semantics:'Separate exact child during lock loss; no current selection inferred'}};
 }
 const tid=target.add(0x38).readU32(),ts=target.add(0x48).readU64().toString();
 if(tid<1||tid>1048576||ts==='0'||!slots.add((tid-1)*8).readPointer().equals(target)||stamps.add((tid-1)*8).readU64().toString()!==ts)return {valid:false,reason:'stale target handle'};
 const actor=target.add(0x70).readPointer();if(actor.isNull()||!matches(target,actor,tid,ts))return {valid:false,reason:'target scene mismatch'};
 const vt=actor.readPointer(),vr=vt.sub(m.base).toString(),type=types[vr],mapped=type!==undefined;
 const parameter=mapped?actor.add(0x1598).readPointer():null;if(mapped&&parameter.isNull())return {valid:false,reason:'missing parameter'};
 const readProfile=()=>mapped?readParts(actor,type,parameter,target):{plausible:false,referenceChild:readChildReference(lastChild,worldKey,matches),semantics:'Unmapped current target; retained child is a separate identity'};
 const profile=readProfile(),profileSignature=JSON.stringify(profile);
 const lb=mapped?actor.add(0x788).readPointer():null,le=mapped?actor.add(0x790).readPointer():null,ln=mapped?vector(lb,le,48,512):0,locks=Array.from({length:ln},(_,i)=>{const p=lb.add(i*48);return {index:i,id:p.readS32(),flags28:p.add(0x28).readU8(),enabled29:p.add(0x29).readU8()};});
 const stable=matches(scene,direct,id,stamp)&&matches(target,actor,tid,ts)&&actor.readPointer().equals(vt)&&(!mapped||actor.add(0x1598).readPointer().equals(parameter))&&JSON.stringify(readProfile())===profileSignature&&(!mapped||actor.add(0x788).readPointer().equals(lb)&&actor.add(0x790).readPointer().equals(le))&&locks.every(x=>{const p=lb.add(x.index*48);return p.readS32()===x.id&&p.add(0x28).readU8()===x.flags28&&p.add(0x29).readU8()===x.enabled29;})&&ex.add(0x108).readPointer().equals(target)&&ex.add(0x118).readS32()===selected&&worldStable();
 if(stable&&mapped)lastChild={worldKey,scene:target,actor,id:tid,stamp:ts,parameter,type,vtable:vt};
 return {valid:stable&&mapped&&profile.plausible,identityValid:stable,profileMapped:mapped,type:type||'Unmapped',vtableRva:vr,actor:actor.toString(),targetHandle:{id:tid,stamp:ts},selected,bodyHpRaw:mapped?actor.add(0x160).readU64().toString():null,flags:mapped?actor.add(0xe).readU8():null,profile,locks};
}
let timer=null,closed=false,ticks=0,events=0,last='';const start=Date.now();
function finish(reason){if(closed)return;closed=true;if(timer!==null)clearInterval(timer);send({kind:'excavallion-child-watch-end',reason,ticks,events,elapsedMs:Date.now()-start,hooks:0,targetCalls:0,gameDataWrites:0});}
function tick(){
 if(closed)return;ticks++;
 try{const s=sample();
  const sig=JSON.stringify(s);if(sig!==last||ticks%20===0){send({kind:'excavallion-child-watch-sample',elapsedMs:Date.now()-start,...s});last=sig;events++;}
 }catch(e){send({kind:'excavallion-child-watch-error',error:String(e)});finish('read exception');}
 if(events>=1800)finish('event limit');if(Date.now()-start>=900000)finish('time limit');
}
rpc.exports={stop(){finish('caller stop');return {closed,ticks,events};}};
send({kind:'excavallion-child-watch-ready',pid:Process.id,maxDurationMs:900000,intervalMs:250,hooks:0,targetCalls:0,gameDataWrites:0});tick();if(!closed)timer=setInterval(tick,250);
