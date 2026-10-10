const fs = require('fs');
const zlib = require('zlib');
const path = require('path');

const unpackedPath = path.join(__dirname, '..', 'unity-webgl', 'Build', 'WebGL.data.unpacked');
const unitywebPath = path.join(__dirname, '..', 'unity-webgl', 'Build', 'WebGL.data.unityweb');

console.log('[Patch] Reading uncompressed WebGL data...');
let buf = fs.readFileSync(unpackedPath);

// Exact length-preserving replacement pairs (same byte length in UTF-8)
function replaceBuffer(target, replacement) {
  const targetBuf = Buffer.isBuffer(target) ? target : Buffer.from(target, 'utf8');
  let repBuf = Buffer.isBuffer(replacement) ? replacement : Buffer.from(replacement, 'utf8');

  if (repBuf.length < targetBuf.length) {
    // Pad with spaces to keep binary alignment and offsets 100% identical
    const pad = Buffer.alloc(targetBuf.length - repBuf.length, 0x20);
    repBuf = Buffer.concat([repBuf, pad]);
  } else if (repBuf.length > targetBuf.length) {
    console.error(`Error: replacement "${replacement}" (${repBuf.length}b) is longer than target "${target}" (${targetBuf.length}b)`);
    return 0;
  }

  let count = 0;
  let pos = 0;
  while ((pos = buf.indexOf(targetBuf, pos)) !== -1) {
    repBuf.copy(buf, pos);
    count++;
    pos += repBuf.length;
  }
  return count;
}

const replacements = [
  // HUD & Controls
  ['ÁRBOLES 0/0', 'TREES 0/0  '],
  ['SEMILLA', 'SEED   '],
  ['RAYO', 'BEAM'],
  ['PAUSA', 'PAUSE'],
  ['Música', 'Music  '],
  ['Efectos', 'Effects'],
  ['Vibración', 'Vibration '],
  ['CONTINUAR', 'RESUME   '],
  ['Joystick fijo', 'Fixed joypad '],
  ['DIFICULTAD: NORMAL', 'DIFFICULTY: NORMAL'],
  ['DIFICULTAD', 'DIFFICULTY'],
  ['SALTAR', 'SKIP  '],
  ['SIGUIENTE', 'NEXT     '],
  ['VOLVER', 'BACK  '],

  // Level Selection
  ['ELIGE NIVEL', 'SELECT ZONE'],
  ['1 · CENTRO DE VILLALIMPIA', '1 · DOWNTOWN DISTRICT    '],
  ['PRÓXIMAMENTE', 'COMING SOON '],

  // Tutorial & Prompts
  ['¡Hola, piloto!', 'Hello, pilot! '],
  ['Buster-1', 'Verdant1'],
  ['¡DISPARA!', 'BEAM!    '],
  ['¡SIEMBRA!', 'SEED!    '],
  ['¡A JUGAR!', "LET'S FLY"],
  ['¡AIRE PURO!', 'CLEAN AIR! '],
  ['¡ALERTA AMBIENTAL!', 'SMOG CRITICAL!    '],
  ['Tu dron Buster-1 puede limpiarla.', 'Your Verdant drone can clean it. '],
  ['La ciudad está llena de smog.', 'The city is smothered in smog.']
];

console.log('[Patch] Applying English string patches...');
for (const [es, en] of replacements) {
  const count = replaceBuffer(es, en);
  if (count > 0) {
    console.log(`  [Patched ${count}x] "${es}" -> "${en}"`);
  }
}

console.log('[Patch] Compressing to WebGL.data.unityweb (gzip)...');
const gzipped = zlib.gzipSync(buf, { level: 9 });
fs.writeFileSync(unitywebPath, gzipped);
console.log(`[Patch] Successfully updated WebGL.data.unityweb (${gzipped.length} bytes)!`);
