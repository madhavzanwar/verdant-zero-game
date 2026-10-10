const express = require('express');
const cors = require('cors');
const path = require('path');
const fs = require('fs');

const app = express();
const PORT = process.env.PORT || 3000;

app.use(cors());
app.use(express.json());

// In-memory persistent data store
let highScores = [
  { id: '1', level: 'Downtown', playerName: 'EcoPilot-1', score: 18450, cloudsPurified: 24, treesPlanted: 16, timeRemaining: 48.2, difficulty: 'NORMAL', date: new Date().toISOString() },
  { id: '2', level: 'Downtown', playerName: 'GreenSky', score: 15200, cloudsPurified: 20, treesPlanted: 14, timeRemaining: 32.5, difficulty: 'NORMAL', date: new Date().toISOString() },
  { id: '3', level: 'Industrial District', playerName: 'AeroZero', score: 21300, cloudsPurified: 28, treesPlanted: 18, timeRemaining: 55.0, difficulty: 'HARD', date: new Date().toISOString() },
];

let playerSettings = {
  musicVolume: 0.8,
  sfxVolume: 0.8,
  vibration: true,
  fixedJoystick: false,
  fullscreen: true,
  firstPersonView: false,
  difficulty: 'NORMAL'
};

// Middleware and static file serving for authentic Unity WebGL build
const unityWebglPath = path.join(__dirname, 'unity-webgl');
if (fs.existsSync(unityWebglPath)) {
  app.use((req, res, next) => {
    if (req.url.endsWith('.unityweb')) {
      res.setHeader('Content-Encoding', 'gzip');
      if (req.url.endsWith('.wasm.unityweb')) {
        res.setHeader('Content-Type', 'application/wasm');
      } else if (req.url.endsWith('.framework.js.unityweb')) {
        res.setHeader('Content-Type', 'application/javascript');
      } else if (req.url.endsWith('.data.unityweb')) {
        res.setHeader('Content-Type', 'application/octet-stream');
      }
    }
    next();
  });
  // Serve authentic Unity WebGL build directly on /
  app.use(express.static(unityWebglPath));
}

// Optional secondary path for Three.js web runner
app.use('/simulator', express.static(path.join(__dirname, 'public')));

// Directly expose Unity Assets for real audio, images, fonts, and materials
const unityAssetsPath = path.join(__dirname, 'VerdantZero', 'Assets', 'VerdantZero');
if (fs.existsSync(unityAssetsPath)) {
  app.use('/unity-assets', express.static(unityAssetsPath));
}

// ---------------- REST APIs ----------------

// Health check endpoint
app.get('/api/health', (req, res) => {
  res.json({
    status: 'healthy',
    game: 'Verdant Zero',
    version: '1.0.0',
    engine: 'Unity 6 (URP) & Three.js Web Companion',
    uptime: process.uptime(),
    timestamp: new Date().toISOString()
  });
});

// Game Info endpoint
app.get('/api/game-info', (req, res) => {
  res.json({
    title: 'Verdant Zero',
    tagline: 'Purify. Reforest. Save the City.',
    synopsis: 'Pilot the Verdant-1 quadcopter drone over a smog-polluted metropolis. Break up toxic pollution clouds with the high-energy Purifier Beam, launch Instant Seed capsules into green planters to restore self-cleaning trees, and reduce city smog to 0% before time runs out.',
    drone: {
      model: 'Verdant-1 Eco Quadcopter',
      maxSpeed: '12 m/s',
      climbSpeed: '5 m/s',
      flightCeiling: '60 m',
      weapons: ['Purifier Beam (Raycast Auto-Aim)', 'Instant Seed Launcher (Parabolic Arc)']
    },
    difficulties: [
      { id: 'EASY', smogRiseRate: '1% per 5.4s', timeLimit: 150, assistAngle: '20° horiz / 40° vert' },
      { id: 'NORMAL', smogRiseRate: '1% per 3.0s', timeLimit: 120, assistAngle: '20° horiz / 40° vert' },
      { id: 'HARD', smogRiseRate: '1% per 2.7s', timeLimit: 90, assistAngle: '10° horiz / 20° vert', microCloudSpeed: '4x' }
    ],
    levels: [
      { id: 'level-1', name: 'Downtown District', size: '3x3 City Grid', difficulty: 'Normal', plantingSpots: 18, smogClouds: 25 },
      { id: 'level-2', name: 'Industrial Quarter', size: 'Chimney Smog Belt', difficulty: 'Hard', plantingSpots: 14, smogClouds: 32 },
      { id: 'level-3', name: 'Metropolis Heights', size: 'Skyline Boulevard', difficulty: 'Expert', plantingSpots: 22, smogClouds: 40 }
    ],
    audioFiles: [
      { type: 'BGM', name: 'main_menu_music_loop.wav', path: '/unity-assets/Audio/BGM/main_menu_music_loop.wav' },
      { type: 'BGM', name: 'game_music_base.wav', path: '/unity-assets/Audio/BGM/game_music_base.wav' },
      { type: 'BGM', name: 'game_music_tension.wav', path: '/unity-assets/Audio/BGM/game_music_tension.wav' },
      { type: 'SFX', name: 'sfx_drone_loop.wav', path: '/unity-assets/Audio/SFX/sfx_drone_loop.wav' },
      { type: 'SFX', name: 'sfx_beam_loop.wav', path: '/unity-assets/Audio/SFX/sfx_beam_loop.wav' },
      { type: 'SFX', name: 'sfx_cloud_purify.wav', path: '/unity-assets/Audio/SFX/sfx_cloud_purify.wav' },
      { type: 'SFX', name: 'sfx_seed_launch.wav', path: '/unity-assets/Audio/SFX/sfx_seed_launch.wav' },
      { type: 'SFX', name: 'sfx_bloom.wav', path: '/unity-assets/Audio/SFX/sfx_bloom.wav' },
      { type: 'SFX', name: 'sfx_smog_alert.wav', path: '/unity-assets/Audio/SFX/sfx_smog_alert.wav' },
      { type: 'VOICE', name: 'Letscleantheairofthiscity.mp3', path: '/unity-assets/Audio/SFX/Letscleantheairofthiscity.mp3' },
      { type: 'VOICE', name: 'Theairisalmostcleanedup.mp3', path: '/unity-assets/Audio/SFX/Theairisalmostcleanedup.mp3' },
      { type: 'VOICE', name: 'running_out_of_time_sound.mp3', path: '/unity-assets/Audio/SFX/running_out_of_time_sound.mp3' },
      { type: 'VOICE', name: 'Lookatthebluesky.mp3', path: '/unity-assets/Audio/SFX/Lookatthebluesky.mp3' }
    ]
  });
});

// Player Settings API
app.get('/api/settings', (req, res) => {
  res.json(playerSettings);
});

app.post('/api/settings', (req, res) => {
  const updates = req.body || {};
  playerSettings = { ...playerSettings, ...updates };
  res.json({ success: true, settings: playerSettings });
});

// High Scores API
app.get('/api/scores', (req, res) => {
  const { level, difficulty } = req.query;
  let filtered = [...highScores];
  if (level) filtered = filtered.filter(s => s.level.toLowerCase() === level.toLowerCase());
  if (difficulty) filtered = filtered.filter(s => s.difficulty.toLowerCase() === difficulty.toLowerCase());
  filtered.sort((a, b) => b.score - a.score);
  res.json(filtered);
});

app.post('/api/scores', (req, res) => {
  const { playerName, score, level, cloudsPurified, treesPlanted, timeRemaining, difficulty } = req.body;
  if (!score || typeof score !== 'number') {
    return res.status(400).json({ error: 'Valid score is required' });
  }
  const newEntry = {
    id: String(Date.now()),
    playerName: (playerName || 'Pilot').trim().slice(0, 20),
    score,
    level: level || 'Downtown',
    cloudsPurified: cloudsPurified || 0,
    treesPlanted: treesPlanted || 0,
    timeRemaining: timeRemaining || 0,
    difficulty: difficulty || 'NORMAL',
    date: new Date().toISOString()
  };
  highScores.push(newEntry);
  highScores.sort((a, b) => b.score - a.score);
  if (highScores.length > 50) highScores = highScores.slice(0, 50);
  res.status(201).json({ success: true, entry: newEntry });
});

// Fallback to index.html for SPA client navigation
app.get('*', (req, res) => {
  res.sendFile(path.join(__dirname, 'public', 'index.html'));
});

// Start listening with port fallback
function startServer(port) {
  const srv = app.listen(port, () => {
    console.log(`[Verdant Zero] Server online and running at http://localhost:${port}`);
  });
  srv.on('error', (err) => {
    if (err.code === 'EADDRINUSE') {
      console.log(`[Verdant Zero] Port ${port} is in use, trying port ${port + 1}...`);
      startServer(port + 1);
    } else {
      console.error('[Verdant Zero] Server error:', err);
    }
  });
}

if (require.main === module) {
  startServer(PORT);
}

module.exports = app;

