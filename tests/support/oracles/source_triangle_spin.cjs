// Independent original-original source DKT operators, strict .fem input, no product imports.
const fs = require('node:fs'), path = require('node:path'), vm = require('node:vm');
const crypto = require('node:crypto'), root = path.resolve(__dirname,'../../..');
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
  // Replace the original nodal rotation-difference penalty by the explicitly
  // specified integral G*t/1000*(theta.n - curl(u).n/2)^2. Physical DKT
  // membrane/bending operators are still executed from the original source.
  const nvec = new context.THREE.Vector3().crossVectors(p[1].clone().sub(p[0]),p[2].clone().sub(p[0]));
  const twiceArea=nvec.length(), area=twiceArea/2; nvec.divideScalar(twiceArea);
  const normalComponents=[nvec.x,nvec.y,nvec.z];
  const originalPenalty=1e-3*t*t*t*area*d[2][2];
  for(let i=0;i<3;i++) for(let j=0;j<3;j++)
    for(let a=0;a<3;a++) for(let b=0;b<3;b++)
      K[6*i+3+a][6*j+3+b]-=originalPenalty*(i===j?1:-.5)*normalComponents[a]*normalComponents[b];
  const gradients=p.map((_,i)=>new context.THREE.Vector3().crossVectors(nvec,p[(i+2)%3].clone().sub(p[(i+1)%3])).divideScalar(twiceArea));
  for(const shape of [[2/3,1/6,1/6],[1/6,2/3,1/6],[1/6,1/6,2/3]]) {
    const row=Array(18).fill(0);
    for(let i=0;i<3;i++) {
      const spin=new context.THREE.Vector3().crossVectors(nvec,gradients[i]).multiplyScalar(-.5);
      [spin.x,spin.y,spin.z].forEach((v,a)=>row[6*i+a]=v);
      normalComponents.forEach((v,a)=>row[6*i+3+a]=shape[i]*v);
    }
    for(let i=0;i<18;i++) for(let j=0;j<18;j++) K[i][j]+=mat[0]/(2*(1+mat[1]))*t*area/3000*row[i]*row[j];
  }
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
