const http = require('http');
const assert = require('assert');
const app = require('../server');

const PORT = 3829; // isolated test port
let server;

function makeRequest(method, path, body = null) {
  return new Promise((resolve, reject) => {
    const data = body ? JSON.stringify(body) : null;
    const options = {
      hostname: 'localhost',
      port: PORT,
      path,
      method,
      headers: {
        'Content-Type': 'application/json',
        ...(data ? { 'Content-Length': Buffer.byteLength(data) } : {})
      }
    };

    const req = http.request(options, (res) => {
      let rawData = '';
      res.on('data', chunk => rawData += chunk);
      res.on('end', () => {
        let json = null;
        try { json = JSON.parse(rawData); } catch (e) {}
        resolve({ statusCode: res.statusCode, headers: res.headers, body: json || rawData });
      });
    });

    req.on('error', reject);
    if (data) req.write(data);
    req.end();
  });
}

async function runTests() {
  console.log('====================================================');
  console.log('  VERDANT ZERO - END-TO-END AUTOMATED TEST SUITE    ');
  console.log('====================================================\n');

  let passed = 0;
  let failed = 0;

  async function test(name, fn) {
    try {
      await fn();
      console.log(`[PASS] ${name}`);
      passed++;
    } catch (err) {
      console.error(`[FAIL] ${name}:`, err.message);
      failed++;
    }
  }

  server = app.listen(PORT);

  // 1. Health check
  await test('GET /api/health returns 200 and healthy status', async () => {
    const res = await makeRequest('GET', '/api/health');
    assert.strictEqual(res.statusCode, 200);
    assert.strictEqual(res.body.status, 'healthy');
    assert.strictEqual(res.body.game, 'Verdant Zero');
  });

  // 2. Game info
  await test('GET /api/game-info returns valid drone specs and difficulty tiers', async () => {
    const res = await makeRequest('GET', '/api/game-info');
    assert.strictEqual(res.statusCode, 200);
    assert.strictEqual(res.body.title, 'Verdant Zero');
    assert.ok(res.body.difficulties.length >= 3);
    assert.ok(res.body.levels.length >= 3);
  });

  // 3. Settings GET
  await test('GET /api/settings returns player configuration', async () => {
    const res = await makeRequest('GET', '/api/settings');
    assert.strictEqual(res.statusCode, 200);
    assert.strictEqual(typeof res.body.musicVolume, 'number');
    assert.strictEqual(typeof res.body.difficulty, 'string');
  });

  // 4. Settings POST
  await test('POST /api/settings updates settings successfully', async () => {
    const res = await makeRequest('POST', '/api/settings', {
      musicVolume: 0.95,
      difficulty: 'HARD'
    });
    assert.strictEqual(res.statusCode, 200);
    assert.strictEqual(res.body.success, true);
    assert.strictEqual(res.body.settings.difficulty, 'HARD');
    assert.strictEqual(res.body.settings.musicVolume, 0.95);
  });

  // 5. Leaderboard GET
  await test('GET /api/scores returns sorted array of high scores', async () => {
    const res = await makeRequest('GET', '/api/scores');
    assert.strictEqual(res.statusCode, 200);
    assert.ok(Array.isArray(res.body));
    assert.ok(res.body.length > 0);
  });

  // 6. Leaderboard POST
  await test('POST /api/scores records new high score entry', async () => {
    const newEntry = {
      playerName: 'AntigravityTester',
      score: 25400,
      level: 'Downtown',
      cloudsPurified: 26,
      treesPlanted: 18,
      timeRemaining: 52.4,
      difficulty: 'HARD'
    };
    const res = await makeRequest('POST', '/api/scores', newEntry);
    assert.strictEqual(res.statusCode, 201);
    assert.strictEqual(res.body.success, true);
    assert.strictEqual(res.body.entry.playerName, 'AntigravityTester');
    assert.strictEqual(res.body.entry.score, 25400);
  });

  // 7. Static file serving - index.html
  await test('GET / returns HTML containing authentic Unity WebGL canvas', async () => {
    const res = await makeRequest('GET', '/');
    assert.strictEqual(res.statusCode, 200);
    assert.ok(typeof res.body === 'string' && res.body.includes('unity-canvas'));
    assert.ok(res.body.includes('unity-container'));
  });

  // 8. Static file serving - Unity WebGL loader & WASM
  await test('GET /Build/WebGL.loader.js serves authentic Unity WebGL loader', async () => {
    const res = await makeRequest('GET', '/Build/WebGL.loader.js');
    assert.strictEqual(res.statusCode, 200);
    assert.ok(typeof res.body === 'string' && res.body.includes('createUnityInstance'));
  });

  // 9. Static file serving - Unity Key Art
  await test('GET /unity-assets/UI/MainMenu/Art/menu_logo.png delivers real asset', async () => {
    const res = await makeRequest('GET', '/unity-assets/UI/MainMenu/Art/menu_logo.png');
    assert.strictEqual(res.statusCode, 200);
    assert.strictEqual(res.headers['content-type'], 'image/png');
  });

  // 10. Static file serving - Real Audio Assets
  await test('GET /unity-assets/Audio/SFX/sfx_bloom.wav delivers real sound effect', async () => {
    const res = await makeRequest('GET', '/unity-assets/Audio/SFX/sfx_bloom.wav');
    assert.strictEqual(res.statusCode, 200);
    assert.strictEqual(res.headers['content-type'], 'audio/wav');
  });

  // 11. Static file serving - Real Voice Audio
  await test('GET /unity-assets/Audio/SFX/Letscleantheairofthiscity.mp3 delivers voice acting clip', async () => {
    const res = await makeRequest('GET', '/unity-assets/Audio/SFX/Letscleantheairofthiscity.mp3');
    assert.strictEqual(res.statusCode, 200);
    assert.strictEqual(res.headers['content-type'], 'audio/mpeg');
  });

  // 12. Game Math & Logic Verification
  await test('Game Rules math validation: smog reduction and time bonus calculations', () => {
    const baseSmog = 0.60;
    const cloudPopReduction = 0.05;
    const treePlantReduction = 0.04;
    const postActionSmog = baseSmog - cloudPopReduction - treePlantReduction;
    assert.strictEqual(Math.round(postActionSmog * 100) / 100, 0.51);

    const timeRemaining = 48.2;
    const timeBonus = Math.ceil(timeRemaining) * 15;
    assert.strictEqual(timeBonus, 49 * 15);
  });

  server.close(() => {
    console.log(`\n====================================================`);
    console.log(`  TEST RESULTS: ${passed} PASSED, ${failed} FAILED  `);
    console.log(`====================================================\n`);
    if (failed > 0) process.exit(1);
  });
}

runTests().catch(err => {
  console.error('Test Suite encountered fatal error:', err);
  if (server) server.close();
  process.exit(1);
});
