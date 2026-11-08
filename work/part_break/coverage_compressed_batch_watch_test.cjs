const assert = require('node:assert/strict');
const fs = require('node:fs'), vm = require('node:vm'), path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, 'coverage_compressed_batch_watch.js'), 'utf8');
const code = source.slice(source.indexOf('function countBytes('), source.indexOf('function sample('));
function fixture(type) {
  const memory = new Map();
  class Pointer {
    constructor(value) { this.value = BigInt(value); }
    add(value) { return new Pointer(this.value + BigInt(value)); }
    sub(value) { return new Pointer(this.value - value.value); }
    equals(value) { return this.value === value.value; }
    isNull() { return this.value === 0n; }
    toString() { return '0x' + this.value.toString(16); }
    read() { assert(memory.has(this.value), 'unprepared memory ' + this.toString()); const value=memory.get(this.value);return typeof value==='function'?value():value; }
    readPointer() { return new Pointer(this.read()); }
    readS32() { return Number(this.read()); }
    readU8() { return Number(this.read()); }
    readU32() { return Number(this.read()); }
    readU64() { return BigInt(this.read()); }
    readFloat() { return Number(this.read()); }
  }
  const p = value => new Pointer(value), put = (base, offset, value) => memory.set(base.value + BigInt(offset), value);
  const actor = p(0x100000), parameter = p(0x200000), target = p(0x300000);
  if (type === 'Em1900') put(actor, 0x9996, 0);
  if (type === 'Em7001') { put(actor, 0xcd8, 0); put(actor, 0xce0, 0); }
  if (type === 'Em7600') { put(actor, 0, 0xf000000n+0x5b24f58n); put(actor, 0xcc8, 0); put(actor, 0x2380, 0); }
  if (type === 'Em7500') {
    put(actor, 0x2288, 1);
    const c = actor.add(0x21b0), owner = p(0x400000), obj = p(0x500000), context = p(0x600000), states = p(0x700000), defs = p(0x800000);
    put(c, 0, 0x900000); put(c, 8, owner.value); put(owner, 0, obj.value); put(obj, 0x28, context.value); put(context, 0x18, target.value);
    put(c, 0x10, states.value); put(c, 0x18, states.value + 36n); put(c, 0x28, defs.value); put(c, 0x30, defs.value + 168n);
    for (let i = 0; i < 3; i++) { put(states, i * 12, 400); put(states, i * 12 + 4, 400); put(states, i * 12 + 8, 0); put(defs, i * 56 + 4, 77 + i); put(defs, i * 56 + 8, 1); }
  } else {
    const human = type === 'Em7201' || type === 'Em7211', count = human ? 7 : 9, start = human ? 0x2604 : 0x25e8, stride = human ? 12 : 20;
    for (let i = 0; i < count; i++) { put(actor, start + i * stride, 400); put(actor, start + i * stride + 4, 400); put(actor, start + i * stride + 8, 0); }
    for (let i = 0; i < (human ? 5 : 9); i++) put(human ? parameter : actor, (human ? 0x8b8 : 0x25bc) + i * 4, human ? 740 : i);
  }
  const module = p(0xf000000);
  const context = vm.createContext({m:{base:module},types:{'0x5b77238':'Em1900','0x5a9d688':'Em7001'}}); vm.runInContext(code+source.slice(source.indexOf('function sample('),source.indexOf('let timer=')), context);
  return { actor, parameter, target, memory, p, put, module, context, sample: () => context.sample(), read: () => context.readParts(actor, type, parameter, target) };
}
for (const type of ['Em7200', 'Em7210', 'Em7201', 'Em7211']) {
  const f = fixture(type), human = type === 'Em7201' || type === 'Em7211', base = human ? 0x2604 : 0x25e8, stride = human ? 12 : 20;
  assert.equal(f.read().rows.length, human ? 7 : 9); assert.equal(f.read().plausible, true);
  f.put(f.actor, base + 2 * stride, 0); assert.equal(f.read().rows[2].broken, 0);
  f.put(f.actor, base + 2 * stride + 8, 1); assert.equal(f.read().rows[2].broken, 1);
  f.put(f.actor, base + 2 * stride + 8, 2); assert.equal(f.read().plausible, false);
  f.put(f.actor, base + 2 * stride + 8, 0); f.put(f.actor, base, -1); assert.equal(f.read().plausible, false);
}
{
  const f = fixture('Em7500'); assert.equal(f.read().plausible, true); assert.equal(f.read().rows[1].part, 78);
  f.put(f.p(0x700000), 12, 250); f.put(f.p(0x700000), 16, 0);
  assert.equal(f.read().plausible, true); assert.equal(f.read().rows[1].initial, 0);
  f.put(f.p(0x600000), 0x18, 0x301000); assert.throws(f.read, /controller owner mismatch/);
}
for (const [offset, value] of [[0x18, 0x700000 + 24], [0x30, 0x800000 + 65 * 56]]) {
  const f = fixture('Em7500'); f.put(f.actor.add(0x21b0), offset, value); assert.throws(f.read, /vector/);
}
assert(!/Interceptor|NativeFunction|\.write(?:U|S|Pointer|Float|Byte|Int)|Memory\.alloc/.test(source));
assert(source.includes('events>=1800') && source.includes('>=900000') && source.includes('clearInterval(timer)'));
{
  const f=fixture('Em3100'),h=f.p(0x710000);f.put(f.actor,0xcd8,h.value);f.put(f.actor,0xce0,h.value+4n);f.put(h,0,2);
  for(let i=0;i<7;i++){f.put(f.actor,0x25b4+i*4,3300-i*100);f.put(f.actor,0x25d0+i*4,3300);}
  assert.equal(f.read().rows[2].current,3100);assert.equal(f.read().history[0],2);
  f.put(f.actor,0xce0,h.value+32n);assert.throws(f.read,/bounds/);
}
{
  const f=fixture('Em1900');for(let i=0;i<14;i++){const o=0x6a68+i*40;for(const [d,v] of [[0,900-i],[4,1000],[8,1],[12,8+i],[16,i]])f.put(f.actor,o+d,v);}
  f.put(f.actor,0x9420,750);f.put(f.parameter,0xbf0,2000);f.put(f.actor,0x9410,1);
  assert.equal(f.read().legDamagePhaseRaw,1);f.put(f.actor,0x9410,3);assert.equal(f.read().legDamagePhaseRaw,3);
  assert.equal(f.read().rows.length,14);assert.equal(f.read().specialCurrent,750);assert.equal(f.read().specialConfigured,2000);
}
{
  const f=fixture('Em7001'),head=f.p(0x410000),node=f.p(0x420000),component=f.p(0x430000),ih=f.p(0x440000),entry=f.p(0x450000),bh=f.p(0x460000);
  f.put(f.module,0x7c28684,-2147483647);
  const owner=f.p(0x470000),obj=f.p(0x480000),ctx=f.p(0x490000);f.put(f.actor,0x648,owner.value);f.put(owner,0,obj.value);f.put(obj,0x28,ctx.value);f.put(ctx,0x18,f.target.value);f.put(f.target,0x70,f.actor.value);
  f.put(f.module,0x7c28680,3);f.put(f.actor,0xc8,0);f.put(f.actor,0xd0,head.value);f.put(f.actor,0xe0,0);
  f.put(head,8,node.value);f.put(head,0x19,1);f.put(node,0x19,0);f.put(node,0,head.value);f.put(node,0x10,head.value);f.put(node,0x20,3);f.put(node,0x28,component.value);
  f.put(component,0x98,ih.value);f.put(ih,0,entry.value);f.put(entry,0,ih.value);f.put(entry,0x10,0x76033489);f.put(entry,0x14,17000);
  f.put(component,0xd8,bh.value);f.put(bh,0,bh.value);f.put(f.parameter,0x5978,17000);
  assert.equal(f.read().blackboard.integers[0].value,17000);assert.equal(f.read().configuredHead,17000);
  f.put(f.actor,0xc8,1);assert.equal(f.read().blackboard.available,false);f.put(f.actor,0xc8,0);
  f.put(entry,0,entry.value);assert.throws(f.read,/list bounds/);f.put(entry,0,ih.value);
  f.put(node,0,node.value);assert.throws(f.read,/tree bounds/);f.put(node,0,head.value);
  f.put(node,0x20,4);assert.equal(f.read().blackboard.available,false);
}
for(const type of ['Em1500','Em7600']){
 const f=fixture(type);f.put(f.module,0x7c28684,-2147483647);f.put(f.module,0x7c28680,3);f.put(f.actor,0xc8,0);f.put(f.actor,0xd0,0);f.put(f.actor,0xe0,0);
 if(type==='Em1500'){f.put(f.actor,0x2898,1);f.put(f.actor,0x289c,45);assert.equal(f.read().golemBehaviorCounter.value,45);}
 assert.equal(f.read().blackboard.available,false);
}
{
 const f=fixture('Em7101'),c=f.actor.add(0x1a78),h=f.p(0x710000),scene=f.p(0x720000),child=f.p(0x730000),manager=f.p(0x740000),slots=f.p(0x750000),stamps=f.p(0x760000);
 f.put(f.target,0x38,4);f.put(f.target,0x48,200);
 for(const [o,v] of [[8,4],[0x10,f.target.value],[0x18,200],[0x20,h.value],[0x28,h.value+24n],[0x58,f.parameter.value+0x6b0n]])f.put(c,o,v);
 for(const [o,v] of [[0,5],[8,scene.value],[16,300]])f.put(h,o,v);
 for(const [o,v] of [[0x38,5],[0x40,scene.value],[0x48,300],[0x70,child.value]])f.put(scene,o,v);
 for(const [o,v] of [[0,f.module.value+0x5d825e0n],[0x5e0,0],[0x5c0,250],[0x5c4,125],[0x368,4],[0x370,f.target.value],[0x378,200]])f.put(child,o,v);
 f.put(f.module,0x7020988,manager.value);f.put(manager,0x20,slots.value);f.put(manager,0x48,stamps.value);f.put(slots,32,scene.value);f.put(stamps,32,300);
 assert.equal(f.read().rows[0].current,125);assert.equal(f.read().plausible,true);
 f.put(stamps,32,301);assert.equal(f.read().plausible,false);f.put(stamps,32,300);
 f.put(child,0x378,201);assert.equal(f.read().plausible,false);f.put(child,0x378,200);
 f.put(c,0x28,h.value+25n);assert.throws(f.read,/bounds/);
}
console.log('PASS: legacy profiles plus Pyet, Tayu raw records, Proto typed blackboard, registry lock/miss and cycle bounds; no target calls/writes');

module.exports={fixture};
