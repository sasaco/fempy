// Independent original-V0 DKT operators, strict .fem input, no product imports.
const fs = require('node:fs'), path = require('node:path'), vm = require('node:vm');
const crypto = require('node:crypto'), root = path.resolve(__dirname,'..');
const context = vm.createContext({console}), hashes = {};
function read(file) {
  const bytes = fs.readFileSync(path.resolve(root,file));
  hashes[file] = crypto.createHash('sha256').update(bytes).digest('hex');
  return bytes.toString('utf8');
}
for (const file of ['lib/three.min.js','lib/numeric-1.2.6.min.js',
    'src/FemMain.js','src/FemDataModel.js','src/Element.js','src/Material.js',
    'src/ElementBorder.js','src/ShellElement.js'])
  vm.runInContext(read('docs/v0/'+file),context,{filename:file});
const source = path.relative(root,path.resolve(process.argv[2])).replaceAll('\\','/');
const nodes={}, materials={}, thickness={}, rests={}, loads={}, elements=[], seen=new Set();
const lengths={Node:3,Material:6,ShellParameter:1,TriElement1:5,Restraint:12,Load:6};
for (const line of read(source).split(/\r?\n/)) {
  const [kind,id,...fields]=line.split('#')[0].trim().split(/\s+/);
  if (!kind) continue;
  if (!(kind in lengths) || (fields.length!==lengths[kind] && !(kind==='Load' && fields.length===3)) || !id ||
      fields.some(v=>!Number.isFinite(Number(v)))) throw Error('Unsupported/malformed '+kind);
  if (kind!=='Load' && seen.has(kind+'/'+id)) throw Error('Duplicate '+kind+'/'+id);
  seen.add(kind+'/'+id);
  const v=fields.map(Number);
  if (kind==='Node') nodes[id]=v;
  if (kind==='Material') materials[id]=v;
  if (kind==='ShellParameter') thickness[id]=v[0];
  if (kind==='Restraint') {
    if ([0,2,4,6,8,10].some(i=>![0,1].includes(v[i]))) throw Error('Restraint flag');
    rests[id]=v;
  }
  if (kind==='Load') loads[id]=Array.from({length:6},(_,i)=>(v[i]||0)+(loads[id]?.[i]||0));
  if (kind==='TriElement1') elements.push({id,material:fields[0],param:fields[1],nodes:fields.slice(2)});
}
for (const id of [...Object.keys(loads),...Object.keys(rests)])
  if (!(id in nodes)) throw Error('Unknown node '+id);
const nodeIds=Object.keys(nodes), offsets=Object.fromEntries(nodeIds.map((n,i)=>[n,6*i]));
const rows=Array.from({length:6*nodeIds.length},()=>new Map()), operators=[];
for (const el of elements) {
  if (!(el.material in materials) || !(el.param in thickness) || el.nodes.some(n=>!(n in nodes)))
    throw Error('Incomplete element');
  const mat=materials[el.material];
  const m=new context.Material(Number(el.material),mat[0],mat[1],mat[3],mat[4],mat[5]);
  const p=el.nodes.map(n=>new context.THREE.Vector3(...nodes[n]));
  const e=new context.TriElement1(Number(el.id),0,0,el.nodes.map(Number));
  const t=thickness[el.param], d=m.matrix2Dstress();
  const K=e.stiffnessMatrix(p,d,{thickness:t});
  const indices=el.nodes.flatMap(n=>Array.from({length:6},(_,i)=>offsets[n]+i));
  K.forEach((row,i)=>row.forEach((v,j)=>{
    const target=rows[indices[i]], column=indices[j];
    target.set(column,(target.get(column)||0)+v);
  }));
  const basis=context.dirMatrix(p), normal=context.normalVector(p);
  const points=[[0,0],[1,0],[0,1],[1/6,1/6],[2/3,1/6],[1/6,2/3]];
  const fields=points.map(([x,y])=>{
    const sf=e.shapeFunction(x,y), sf3=e.shapeFunction3(p,basis,x,y);
    const inv=e.jacobInv(e.jacobianMatrix(p,sf,normal,t),basis);
    return [1,-1].map(sign=>e.strainMatrix(sf,sf3,inv,basis,sign,t));
  });
  operators.push({id:el.id,nodes:el.nodes,indices,basis,elastic:d,fields});
}
console.log(JSON.stringify({source,hashes,node_ids:nodeIds,operators,
  stiffness_rows:rows.map(row=>[...row].filter(([j,v])=>v!==0)),
  loads:nodeIds.flatMap(n=>loads[n]||Array(6).fill(0)),
  prescribed:Object.fromEntries(nodeIds.flatMap(n=>Array.from({length:6},(_,i)=>i)
    .filter(i=>rests[n]?.[2*i]).map(i=>[offsets[n]+i,rests[n][2*i+1]]))),
  restraint_nodes:Object.keys(rests)}));
