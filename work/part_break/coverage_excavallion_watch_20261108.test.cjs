const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm'),path=require('node:path');
const {fixture}=require('./coverage_compressed_batch_watch_test.cjs');
const candidate=path.join(__dirname,'coverage_excavallion_watch_20261108.js');
const source=fs.readFileSync(fs.existsSync(candidate)?candidate:path.join(__dirname,'coverage_vulkan_spawn_watch_20261107.js'),'utf8');
const tables={Em7401:[0x6163b50n,0x5bf0808n],Em7404:[0x6164a90n,0x5bf2c68n],Em7406:[0x6165230n,0x5bf4108n],Em7407:[0x61659d0n,0x5d9eed8n]};
function setup(type){
 const f=fixture(type),manager=f.p(0x900000),slots=f.p(0x910000),stamps=f.p(0x920000),parent=f.p(0xa00000),parentActor=f.p(0xb00000);
 f.put(f.module,0x7020988,manager.value);f.put(manager,0x20,slots.value);f.put(manager,0x48,stamps.value);
 for(const[o,v]of[[0,tables[type][0]+f.module.value],[0x150,tables[type][1]+f.module.value],[0x160,17000],[0x168,17000],[0x1a50,9],[0x1a58,parent.value],[0x1a60,12345]])f.put(f.actor,o,v);
 for(const[o,v]of[[0x38,9],[0x40,parent.value],[0x48,12345],[0x70,parentActor.value]])f.put(parent,o,v);
 f.put(parentActor,0,f.module.value+0x123456n);f.put(slots,64,parent.value);f.put(stamps,64,12345);
 const end=source.indexOf('let lastChild=')>=0?source.indexOf('let lastChild='):source.indexOf('let lastRoot=');
 vm.runInContext(source.slice(source.indexOf('function readParts('),end),f.context);
 return {...f,parent,parentActor,stamps,read:()=>f.context.readParts(f.actor,type,f.parameter,f.target)};
}
for(const type of Object.keys(tables)){
 const f=setup(type),initial=f.read();assert.equal(initial.plausible,true);assert.equal(initial.current,17000);assert.equal(initial.maximum,17000);
 assert.equal(initial.parent.id,9);assert.equal(initial.parent.actor,f.parentActor.toString());
 f.put(f.actor,0x160,16750);assert.equal(f.read().current,16750);assert.equal(f.read().maximum,17000);
 f.put(f.actor,0x160,0);assert.equal(f.read().current,0);assert.equal(f.read().plausible,true);
 assert.equal(f.read().broken,undefined,'raw zero must not invent a processed flag');
 f.put(f.actor,0x160,17001);assert.equal(f.read().plausible,false);
 f.put(f.actor,0x160,0);f.put(f.actor,0x168,0);assert.equal(f.read().plausible,false);
 f.put(f.actor,0x168,9007199254740993n);assert.equal(f.read().plausible,false);assert.equal(f.read().maximumRaw,'9007199254740993');
}
{
 const f=setup('Em7401');f.put(f.stamps,64,54321);f.put(f.parent,0x70,()=>{throw Error('stale parent followed');});assert.equal(f.read().plausible,false);
}
for(const offset of [0,0x150]){
 const f=setup('Em7404');f.put(f.actor,offset,f.module.value+0x777n);f.put(f.actor,0x160,()=>{throw Error('foreign HP followed');});assert.equal(f.read().plausible,false);
}
assert(!/Interceptor|NativeFunction|\.write(?:U|S|Pointer|Float|Byte|Int)|Memory\.alloc/.test(source));
assert(source.includes('events>=1800')&&source.includes('>=900000')&&source.includes('clearInterval(timer)'));
console.log('PASS: four exact child/ExHp types; independent current/max; zero stays raw; invalid and unsafe HP, stale parent and foreign tables rejected; bounded read-only observation');
