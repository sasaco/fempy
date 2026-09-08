// Independent support reactions: original V0 JS stiffness times original .out
// displacements. No src/fem imports, target solver runs, or fixture writes.
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const crypto = require('node:crypto');
const root = path.resolve(__dirname, '..');
const context = vm.createContext({console});
const hashes = {};
function read(relative) {
  const bytes = fs.readFileSync(path.resolve(root, relative));
  hashes[relative] = crypto.createHash('sha256').update(bytes).digest('hex');
  return bytes.toString('utf8');
}
for (const file of ['lib/three.min.js', 'lib/numeric-1.2.6.min.js',
  'src/FemMain.js', 'src/FemDataModel.js', 'src/Element.js', 'src/Material.js',
  'src/ElementBorder.js', 'src/SolidElement.js']) {
  vm.runInContext(read('docs/v0/'+file), context, {filename:file});
}
const source = path.relative(root, path.resolve(process.argv[2])).replaceAll('\\', '/');
const nodes = {}, materials = {}, elements = [], rests = {}, loads = {}, u = {};
const counts = {TetraElement1:4, WedgeElement1:6, HexaElement1:8,
  TetraElement2:10, WedgeElement2:15, HexaElement2:20};
for (const line of read(source).split(/\r?\n/)) {
  const [kind, id, ...fields] = line.trim().split(/\s+/);
  if (kind === 'Node') nodes[id] = fields.map(Number);
  if (kind === 'Material') materials[id] = fields.map(Number);
  if (kind === 'Restraint') rests[id] = fields.map(Number);
  if (kind === 'Load') {
    const values = fields.map(Number);
    loads[id] = Array.from({length:3}, (_, i) => (loads[id]?.[i] || 0)+(values[i] || 0));
  }
  if (kind === 'Displacement') u[id] = fields.map(Number).slice(0,3);
  if (kind?.includes('Element')) {
    if (!(kind in counts) || fields.length !== counts[kind]+1) throw Error('Unsupported source element '+kind);
    elements.push({kind, id, material:fields[0], nodes:fields.slice(1)});
  }
}
if (!elements.length || !Object.keys(rests).length ||
    Object.keys(nodes).length !== Object.keys(u).length ||
    Object.keys(nodes).some(id => !u[id])) throw Error('Complete source input echo and displacements required');
const reactions = Object.fromEntries(Object.keys(nodes).map(id => [id, [0,0,0]]));
const exportSystem = process.argv.includes('--system');
const nodeIds = Object.keys(nodes);
const offsets = Object.fromEntries(nodeIds.map((id, i) => [id, 3*i]));
const rows = exportSystem ? Array.from({length:3*nodeIds.length}, () => new Map()) : null;
for (const el of elements) {
  if (!materials[el.material] || el.nodes.some(id => !nodes[id])) throw Error('Incomplete source model');
  const properties = materials[el.material];
  // FileIO.js reconstructs G from E/nu. The printed G column is not read there.
  const mat = new context.Material(Number(el.material), properties[0], properties[1], properties[3], properties[4], properties[5]);
  const p = el.nodes.map(id => new context.THREE.Vector3(...nodes[id]));
  const element = new context[el.kind](Number(el.id), 0, el.nodes.map(Number));
  const k = element.stiffnessMatrix(p, mat.matrix3D());
  const displacement = el.nodes.flatMap(id => u[id]);
  for (let i=0; i<el.nodes.length; ++i) {
    const id = el.nodes[i];
    for (let j=0; j<3; ++j) {
      const row = k[3*i+j];
      reactions[id][j] += row.reduce((sum, value, index) => sum+value*displacement[index], 0);
      if (exportSystem) {
        const target = rows[offsets[id]+j];
        row.forEach((value, index) => {
          const column = offsets[el.nodes[Math.floor(index/3)]]+index%3;
          target.set(column, (target.get(column) || 0)+value);
        });
      }
    }
  }
}
const reac = {};
let maximumFreeResidual = 0;
for (const [id, reaction] of Object.entries(reactions)) {
  for (let i=0; i<3; ++i) {
    if (!rests[id]?.[2*i]) maximumFreeResidual = Math.max(maximumFreeResidual, Math.abs(reaction[i]-(loads[id]?.[i] || 0)));
  }
  if (!(id in rests)) continue;
  reac[id] = {};
  ['tx','ty','tz'].forEach((key,i) => { reac[id][key] = rests[id][2*i] ? reaction[i]-(loads[id]?.[i] || 0) : 0; });
  Object.assign(reac[id], {mx:0,my:0,mz:0});
}
const output = {source, hashes, method:'V0 JS K times original output displacement minus source nodal load',
  maximum_free_force_residual:maximumFreeResidual, reac};
if (exportSystem) Object.assign(output, {node_ids:nodeIds,
  stiffness_rows:rows.map(row => [...row].filter(([column, value]) => value !== 0)),
  loads:nodeIds.flatMap(id => loads[id] || [0,0,0]),
  displacement:nodeIds.flatMap(id => u[id]),
  prescribed:Object.fromEntries(nodeIds.flatMap(id => [0,1,2].filter(i => rests[id]?.[2*i])
    .map(i => [offsets[id]+i, rests[id][2*i+1]])))});
console.log(JSON.stringify(output));
