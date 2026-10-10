import * as THREE from 'three';

// ==========================================
// VERDANT ZERO - 3D ARCADE GAME ENGINE
// ==========================================

// --- Game State Constants ---
const GameState = {
  MENU: 'MENU',
  TUTORIAL: 'TUTORIAL',
  PLAYING: 'PLAYING',
  PAUSED: 'PAUSED',
  GAMEOVER: 'GAMEOVER'
};

const Difficulties = {
  EASY: { smogInterval: 5.4, timeLimit: 150, assistAngle: 30, microSpeed: 0.5 },
  NORMAL: { smogInterval: 3.0, timeLimit: 120, assistAngle: 20, microSpeed: 1.0 },
  HARD: { smogInterval: 2.7, timeLimit: 90, assistAngle: 10, microSpeed: 2.5 }
};

// --- Audio Synthesizer & Web Audio Manager ---
class SoundSystem {
  constructor() {
    this.ctx = null;
    this.musicVol = 0.8;
    this.sfxVol = 0.8;
    this.droneOsc = null;
    this.droneGain = null;
    this.beamOsc = null;
    this.beamGain = null;
    this.initialized = false;
  }

  init() {
    if (this.initialized) return;
    try {
      const AudioContext = window.AudioContext || window.webkitAudioContext;
      this.ctx = new AudioContext();
      this.initialized = true;
      this.setupContinuousLoops();
    } catch (e) {
      console.warn('Web Audio not supported', e);
    }
  }

  setupContinuousLoops() {
    if (!this.ctx) return;
    // Drone motor engine loop
    this.droneOsc = this.ctx.createOscillator();
    this.droneOsc.type = 'sawtooth';
    this.droneOsc.frequency.setValueAtTime(80, this.ctx.currentTime);
    this.droneGain = this.ctx.createGain();
    this.droneGain.gain.setValueAtTime(0, this.ctx.currentTime);

    // Motor lowpass filter for realistic quadcopter hum
    const filter = this.ctx.createBiquadFilter();
    filter.type = 'lowpass';
    filter.frequency.setValueAtTime(320, this.ctx.currentTime);

    this.droneOsc.connect(filter);
    filter.connect(this.droneGain);
    this.droneGain.connect(this.ctx.destination);
    this.droneOsc.start();

    // Beam continuous pulse
    this.beamOsc = this.ctx.createOscillator();
    this.beamOsc.type = 'sine';
    this.beamOsc.frequency.setValueAtTime(440, this.ctx.currentTime);
    this.beamGain = this.ctx.createGain();
    this.beamGain.gain.setValueAtTime(0, this.ctx.currentTime);

    const beamFilter = this.ctx.createBiquadFilter();
    beamFilter.type = 'bandpass';
    beamFilter.frequency.setValueAtTime(600, this.ctx.currentTime);

    this.beamOsc.connect(beamFilter);
    beamFilter.connect(this.beamGain);
    this.beamGain.connect(this.ctx.destination);
    this.beamOsc.start();
  }

  updateDronePitch(speed, climbRate, isPlaying) {
    if (!this.ctx || !this.droneGain) return;
    if (!isPlaying) {
      this.droneGain.gain.setTargetAtTime(0, this.ctx.currentTime, 0.1);
      return;
    }
    const targetGain = 0.12 * this.sfxVol;
    this.droneGain.gain.setTargetAtTime(targetGain, this.ctx.currentTime, 0.05);

    const basePitch = 85 + (speed / 12) * 65 + climbRate * 12;
    this.droneOsc.frequency.setTargetAtTime(Math.max(60, Math.min(220, basePitch)), this.ctx.currentTime, 0.08);
  }

  setBeamActive(active) {
    if (!this.ctx || !this.beamGain) return;
    const targetGain = active ? 0.22 * this.sfxVol : 0;
    this.beamGain.gain.setTargetAtTime(targetGain, this.ctx.currentTime, 0.05);
    if (active) {
      const now = this.ctx.currentTime;
      this.beamOsc.frequency.setValueAtTime(520, now);
      this.beamOsc.frequency.exponentialRampToValueAtTime(780, now + 0.15);
    }
  }

  playPop() {
    if (!this.ctx || this.sfxVol <= 0) return;
    const osc = this.ctx.createOscillator();
    const gain = this.ctx.createGain();
    osc.type = 'sine';
    const now = this.ctx.currentTime;
    osc.frequency.setValueAtTime(800, now);
    osc.frequency.exponentialRampToValueAtTime(200, now + 0.18);
    gain.gain.setValueAtTime(0.4 * this.sfxVol, now);
    gain.gain.exponentialRampToValueAtTime(0.001, now + 0.18);
    osc.connect(gain);
    gain.connect(this.ctx.destination);
    osc.start();
    osc.stop(now + 0.2);
  }

  playSeedLaunch() {
    if (!this.ctx || this.sfxVol <= 0) return;
    const osc = this.ctx.createOscillator();
    const gain = this.ctx.createGain();
    osc.type = 'triangle';
    const now = this.ctx.currentTime;
    osc.frequency.setValueAtTime(280, now);
    osc.frequency.exponentialRampToValueAtTime(620, now + 0.12);
    gain.gain.setValueAtTime(0.35 * this.sfxVol, now);
    gain.gain.exponentialRampToValueAtTime(0.001, now + 0.15);
    osc.connect(gain);
    gain.connect(this.ctx.destination);
    osc.start();
    osc.stop(now + 0.16);
  }

  playBloom() {
    if (!this.ctx || this.sfxVol <= 0) return;
    const notes = [440, 554, 659, 880];
    notes.forEach((freq, idx) => {
      const osc = this.ctx.createOscillator();
      const gain = this.ctx.createGain();
      osc.type = 'sine';
      const now = this.ctx.currentTime + idx * 0.04;
      osc.frequency.setValueAtTime(freq, now);
      gain.gain.setValueAtTime(0.2 * this.sfxVol, now);
      gain.gain.exponentialRampToValueAtTime(0.001, now + 0.35);
      osc.connect(gain);
      gain.connect(this.ctx.destination);
      osc.start(now);
      osc.stop(now + 0.36);
    });
  }

  playVictory() {
    if (!this.ctx || this.sfxVol <= 0) return;
    const fanfare = [523, 659, 784, 1046];
    fanfare.forEach((freq, idx) => {
      const osc = this.ctx.createOscillator();
      const gain = this.ctx.createGain();
      osc.type = 'triangle';
      const now = this.ctx.currentTime + idx * 0.15;
      osc.frequency.setValueAtTime(freq, now);
      gain.gain.setValueAtTime(0.35 * this.sfxVol, now);
      gain.gain.exponentialRampToValueAtTime(0.001, now + 0.6);
      osc.connect(gain);
      gain.connect(this.ctx.destination);
      osc.start(now);
      osc.stop(now + 0.65);
    });
  }

  playCrash() {
    if (!this.ctx || this.sfxVol <= 0) return;
    const osc = this.ctx.createOscillator();
    const gain = this.ctx.createGain();
    osc.type = 'sawtooth';
    const now = this.ctx.currentTime;
    osc.frequency.setValueAtTime(140, now);
    osc.frequency.exponentialRampToValueAtTime(30, now + 0.4);
    gain.gain.setValueAtTime(0.5 * this.sfxVol, now);
    gain.gain.exponentialRampToValueAtTime(0.001, now + 0.4);
    osc.connect(gain);
    gain.connect(this.ctx.destination);
    osc.start();
    osc.stop(now + 0.42);
  }
}

// --- Main Game App ---
class VerdantZeroGame {
  constructor() {
    this.state = GameState.MENU;
    this.currentDifficulty = 'NORMAL';
    this.currentLevel = 'Downtown';
    this.sound = new SoundSystem();

    // Game stats
    this.smog = 0.6;
    this.timeRemaining = 120;
    this.score = 0;
    this.cloudsPurified = 0;
    this.treesPlanted = 0;
    this.totalPlantingSpots = 18;
    this.smogRemoved = 0;
    this.smogRiseTimer = 0;

    // View modes
    this.isFirstPerson = false;
    this.activeTargetCloud = null;
    this.activeTargetSpot = null;

    // Input state
    this.keys = {};
    this.mouse = { x: 0, y: 0, beam: false, seed: false };
    this.virtualInput = { moveX: 0, moveY: 0, climb: 0, beam: false, seed: false };

    // 3D Objects lists
    this.buildings = [];
    this.smogClouds = [];
    this.plantingSpots = [];
    this.seedProjectiles = [];
    this.rotors = [];

    this.initDOM();
    this.initThree();
    this.buildCity();
    this.buildDrone();
    this.setupListeners();

    // Start render loop
    this.lastTime = performance.now();
    requestAnimationFrame(this.render.bind(this));
  }

  initDOM() {
    this.dom = {
      mainMenu: document.getElementById('main-menu'),
      gameHud: document.getElementById('game-hud'),
      overlayLevels: document.getElementById('overlay-levels'),
      overlayOptions: document.getElementById('overlay-options'),
      overlayCredits: document.getElementById('overlay-credits'),
      overlayTutorial: document.getElementById('overlay-tutorial'),
      overlayPause: document.getElementById('overlay-pause'),
      overlayGameOver: document.getElementById('overlay-gameover'),
      smogVal: document.getElementById('hud-smog-val'),
      smogFill: document.getElementById('hud-smog-fill'),
      timer: document.getElementById('hud-timer'),
      score: document.getElementById('hud-score'),
      trees: document.getElementById('hud-trees'),
      crosshair: document.getElementById('crosshair'),
      voiceLine: document.getElementById('hud-voice-line'),
      boundary: document.getElementById('hud-boundary'),
      camToggle: document.getElementById('btn-cam-toggle'),
      statScore: document.getElementById('stat-score'),
      statSmog: document.getElementById('stat-smog-removed'),
      statClouds: document.getElementById('stat-clouds'),
      statTrees: document.getElementById('stat-trees'),
      gameoverTitle: document.getElementById('gameover-title'),
      gameoverSubtitle: document.getElementById('gameover-subtitle'),
      gameoverPanel: document.getElementById('gameover-panel')
    };
  }

  initThree() {
    const canvas = document.getElementById('three-canvas');
    this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true, powerPreference: 'high-performance' });
    this.renderer.setSize(window.innerWidth, window.innerHeight);
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.shadowMap.enabled = true;
    this.renderer.shadowMap.type = THREE.PCFShadowMap;

    this.scene = new THREE.Scene();
    this.scene.background = new THREE.Color(0x1a2e3b);
    this.scene.fog = new THREE.FogExp2(0x283e4a, 0.015);

    // Primary Camera
    this.camera = new THREE.PerspectiveCamera(65, window.innerWidth / window.innerHeight, 0.2, 500);
    this.camera.position.set(0, 15, 25);

    // Lighting
    this.ambientLight = new THREE.AmbientLight(0xd9efff, 0.75);
    this.scene.add(this.ambientLight);

    this.sunLight = new THREE.DirectionalLight(0xfffaed, 1.3);
    this.sunLight.position.set(60, 100, 40);
    this.sunLight.castShadow = true;
    this.sunLight.shadow.mapSize.width = 2048;
    this.sunLight.shadow.mapSize.height = 2048;
    this.sunLight.shadow.camera.near = 10;
    this.sunLight.shadow.camera.far = 250;
    this.sunLight.shadow.camera.left = -90;
    this.sunLight.shadow.camera.right = 90;
    this.sunLight.shadow.camera.top = 90;
    this.sunLight.shadow.camera.bottom = -90;
    this.scene.add(this.sunLight);

    // Sky Dome with dynamic gradient
    const skyGeo = new THREE.SphereGeometry(300, 32, 16);
    this.skyMat = new THREE.MeshBasicMaterial({ color: 0x1e3646, side: THREE.BackSide });
    this.skyMesh = new THREE.Mesh(skyGeo, this.skyMat);
    this.scene.add(this.skyMesh);

    // Laser Purifier Beam Visual Mesh
    const beamGeo = new THREE.CylinderGeometry(0.12, 0.12, 1, 8);
    beamGeo.rotateX(Math.PI / 2);
    this.beamMat = new THREE.MeshBasicMaterial({ color: 0x00e5ff, transparent: true, opacity: 0 });
    this.beamMesh = new THREE.Mesh(beamGeo, this.beamMat);
    this.scene.add(this.beamMesh);
  }

  buildCity() {
    // Ground asphalt plane
    const groundGeo = new THREE.PlaneGeometry(260, 260);
    const groundMat = new THREE.MeshStandardMaterial({ color: 0x1c242b, roughness: 0.9 });
    const ground = new THREE.Mesh(groundGeo, groundMat);
    ground.rotation.x = -Math.PI / 2;
    ground.receiveShadow = true;
    this.scene.add(ground);

    // Grid layout: 3x3 Blocks with 8m avenues
    const blockSize = 36;
    const roadWidth = 10;
    const halfExtent = (3 * (blockSize + roadWidth)) / 2;

    const buildingColors = [0x273540, 0x1f2e37, 0x2b3d49, 0x18242c, 0x334856];

    let spotId = 0;
    for (let bx = -1; bx <= 1; bx++) {
      for (let bz = -1; bz <= 1; bz++) {
        const cx = bx * (blockSize + roadWidth);
        const cz = bz * (blockSize + roadWidth);

        // Subdivide block into 2-4 varied buildings
        const numSubs = (bx === 0 && bz === 0) ? 1 : 3; // Center block is a large botanical eco-plaza!

        if (bx === 0 && bz === 0) {
          // Central Grand Eco Plaza & Botanical Garden
          const parkGeo = new THREE.BoxGeometry(blockSize - 4, 1.2, blockSize - 4);
          const parkMat = new THREE.MeshStandardMaterial({ color: 0x2e4738, roughness: 0.8 });
          const park = new THREE.Mesh(parkGeo, parkMat);
          park.position.set(0, 0.6, 0);
          park.receiveShadow = true;
          this.scene.add(park);

          // Add multiple planting spots in the central plaza
          for (let pi = -1; pi <= 1; pi++) {
            for (let pj = -1; pj <= 1; pj++) {
              if (pi === 0 && pj === 0) continue;
              this.addPlantingSpot(pi * 10, 1.2, pj * 10, `Plaza Spot #${++spotId}`);
            }
          }
        } else {
          // Commercial & Residential Towers
          const heights = [16 + Math.random() * 24, 20 + Math.random() * 28, 14 + Math.random() * 18];
          const subW = (blockSize - 4) / 2;
          const offsets = [
            { x: -subW / 2, z: -subW / 2 },
            { x: subW / 2, z: -subW / 2 },
            { x: 0, z: subW / 2 }
          ];

          offsets.forEach((off, idx) => {
            const h = heights[idx];
            const bw = subW - 2;
            const bd = subW - 2;
            const bGeo = new THREE.BoxGeometry(bw, h, bd);
            const bMat = new THREE.MeshStandardMaterial({
              color: buildingColors[Math.floor(Math.random() * buildingColors.length)],
              roughness: 0.6,
              metalness: 0.2
            });
            const bMesh = new THREE.Mesh(bGeo, bMat);
            const px = cx + off.x;
            const pz = cz + off.z;
            bMesh.position.set(px, h / 2, pz);
            bMesh.castShadow = true;
            bMesh.receiveShadow = true;
            this.scene.add(bMesh);

            // Bounding box hazard for crash detection
            this.buildings.push({
              minX: px - bw / 2, maxX: px + bw / 2,
              minZ: pz - bd / 2, maxZ: pz + bd / 2,
              height: h
            });

            // Rooftop planter spot on select towers
            if (Math.random() > 0.45 && this.plantingSpots.length < this.totalPlantingSpots) {
              this.addPlantingSpot(px, h, pz, `Rooftop Spot #${++spotId}`);
            }
          });
        }
      }
    }

    // Add perimeter boundary walls for bounds detection
    this.playAreaHalf = halfExtent + 8;

    // Spawn Ambient Smog Clouds
    this.spawnSmogClouds();
  }

  addPlantingSpot(x, y, z, name) {
    const group = new THREE.Group();
    group.position.set(x, y, z);

    // Planter base ring
    const baseGeo = new THREE.CylinderGeometry(2.2, 2.4, 0.4, 16);
    const baseMat = new THREE.MeshStandardMaterial({ color: 0x475569 });
    const baseMesh = new THREE.Mesh(baseGeo, baseMat);
    baseMesh.position.y = 0.2;
    group.add(baseMesh);

    // Soil
    const soilGeo = new THREE.CylinderGeometry(2.0, 2.0, 0.1, 16);
    const soilMat = new THREE.MeshStandardMaterial({ color: 0x2b1d14, roughness: 0.9 });
    const soilMesh = new THREE.Mesh(soilGeo, soilMat);
    soilMesh.position.y = 0.4;
    group.add(soilMesh);

    // Glowing green beacon beam
    const beaconGeo = new THREE.CylinderGeometry(0.3, 1.8, 14, 16, 1, true);
    const beaconMat = new THREE.MeshBasicMaterial({
      color: 0x78ffbe,
      transparent: true,
      opacity: 0.45,
      side: THREE.DoubleSide
    });
    const beacon = new THREE.Mesh(beaconGeo, beaconMat);
    beacon.position.y = 7;
    group.add(beacon);

    // Target highlight halo
    const haloGeo = new THREE.RingGeometry(2.6, 3.2, 24);
    const haloMat = new THREE.MeshBasicMaterial({ color: 0x78ffbe, side: THREE.DoubleSide, transparent: true, opacity: 0 });
    const halo = new THREE.Mesh(haloGeo, haloMat);
    halo.rotation.x = -Math.PI / 2;
    halo.position.y = 0.45;
    group.add(halo);

    this.scene.add(group);

    this.plantingSpots.push({
      id: name,
      position: new THREE.Vector3(x, y + 0.4, z),
      group,
      beacon,
      halo,
      haloMat,
      isPlanted: false,
      treeMesh: null
    });
  }

  spawnSmogClouds() {
    const cloudGeo = new THREE.DodecahedronGeometry(3.5, 1);
    const cloudMat = new THREE.MeshStandardMaterial({
      color: 0x8a7a58,
      roughness: 0.9,
      transparent: true,
      opacity: 0.82
    });

    for (let i = 0; i < 24; i++) {
      const cloudGroup = new THREE.Group();
      // Clusters of 3 spheres per cloud
      for (let s = 0; s < 3; s++) {
        const p = new THREE.Mesh(cloudGeo, cloudMat);
        p.position.set((Math.random() - 0.5) * 3, (Math.random() - 0.5) * 1.5, (Math.random() - 0.5) * 3);
        p.scale.setScalar(0.7 + Math.random() * 0.5);
        cloudGroup.add(p);
      }

      const cx = (Math.random() - 0.5) * (this.playAreaHalf * 1.6);
      const cz = (Math.random() - 0.5) * (this.playAreaHalf * 1.6);
      const cy = 8 + Math.random() * 32;

      cloudGroup.position.set(cx, cy, cz);
      this.scene.add(cloudGroup);

      this.smogClouds.push({
        group: cloudGroup,
        radius: 3.8,
        wanderX: (Math.random() - 0.5) * 0.8,
        wanderZ: (Math.random() - 0.5) * 0.8,
        isMicro: Math.random() > 0.65
      });
    }
  }

  buildDrone() {
    this.drone = new THREE.Group();
    this.drone.position.set(0, 10, 0);

    // Chassis body
    const bodyGeo = new THREE.BoxGeometry(1.6, 0.4, 1.8);
    const bodyMat = new THREE.MeshStandardMaterial({ color: 0x223640, roughness: 0.4, metalness: 0.7 });
    const body = new THREE.Mesh(bodyGeo, bodyMat);
    body.castShadow = true;
    this.drone.add(body);

    // Cockpit canopy / sensor dome
    const sensorGeo = new THREE.SphereGeometry(0.45, 16, 12);
    const sensorMat = new THREE.MeshStandardMaterial({ color: 0x00e5ff, roughness: 0.1, metalness: 0.9 });
    const sensor = new THREE.Mesh(sensorGeo, sensorMat);
    sensor.position.set(0, 0.25, 0.7);
    this.drone.add(sensor);

    // Arms & Rotors (4 quadcopter motors)
    const armGeo = new THREE.CylinderGeometry(0.08, 0.08, 2.4, 8);
    armGeo.rotateZ(Math.PI / 2);
    const armMat = new THREE.MeshStandardMaterial({ color: 0x475569 });

    const arm1 = new THREE.Mesh(armGeo, armMat);
    arm1.rotation.y = Math.PI / 4;
    this.drone.add(arm1);

    const arm2 = new THREE.Mesh(armGeo, armMat);
    arm2.rotation.y = -Math.PI / 4;
    this.drone.add(arm2);

    const motorPositions = [
      { x: 1.1, z: 1.1 },
      { x: -1.1, z: 1.1 },
      { x: 1.1, z: -1.1 },
      { x: -1.1, z: -1.1 }
    ];

    const propGeo = new THREE.BoxGeometry(1.3, 0.02, 0.12);
    const propMat = new THREE.MeshStandardMaterial({ color: 0x78e6aa, roughness: 0.3 });

    motorPositions.forEach((pos) => {
      // Motor pod
      const podGeo = new THREE.CylinderGeometry(0.18, 0.18, 0.3, 8);
      const podMat = new THREE.MeshStandardMaterial({ color: 0x111827 });
      const pod = new THREE.Mesh(podGeo, podMat);
      pod.position.set(pos.x, 0.15, pos.y);
      this.drone.add(pod);

      // Spinning propeller
      const prop = new THREE.Mesh(propGeo, propMat);
      prop.position.set(pos.x, 0.32, pos.y);
      this.drone.add(prop);
      this.rotors.push(prop);
    });

    // Blue Purifier Cannon (Left)
    const beamCannonGeo = new THREE.CylinderGeometry(0.12, 0.12, 0.8, 8);
    beamCannonGeo.rotateX(Math.PI / 2);
    const beamCannonMat = new THREE.MeshStandardMaterial({ color: 0x1aa8c4, roughness: 0.3 });
    const beamCannon = new THREE.Mesh(beamCannonGeo, beamCannonMat);
    beamCannon.position.set(-0.6, -0.15, 0.8);
    this.drone.add(beamCannon);
    this.beamMuzzle = new THREE.Vector3(-0.6, -0.15, 1.2);

    // Green Seed Cannon (Right)
    const seedCannonGeo = new THREE.CylinderGeometry(0.12, 0.12, 0.8, 8);
    seedCannonGeo.rotateX(Math.PI / 2);
    const seedCannonMat = new THREE.MeshStandardMaterial({ color: 0x22c55e, roughness: 0.3 });
    const seedCannon = new THREE.Mesh(seedCannonGeo, seedCannonMat);
    seedCannon.position.set(0.6, -0.15, 0.8);
    this.drone.add(seedCannon);
    this.seedMuzzle = new THREE.Vector3(0.6, -0.15, 1.2);

    this.scene.add(this.drone);

    // Flight dynamic variables
    this.velocity = new THREE.Vector3();
    this.targetVelocity = new THREE.Vector3();
    this.droneAltitude = 10;
    this.droneYaw = 0;
    this.droneTiltX = 0;
    this.droneTiltZ = 0;
    this.hasCrashed = false;
  }

  setupListeners() {
    window.addEventListener('resize', this.onResize.bind(this));

    // Keyboard bindings
    window.addEventListener('keydown', (e) => {
      this.keys[e.key.toLowerCase()] = true;
      if (e.key.toLowerCase() === 'v') this.toggleCameraMode();
      if (e.key === 'Escape') this.togglePause();
      if (e.key === 'F11') {
        e.preventDefault();
        this.toggleFullscreen();
      }
    });

    window.addEventListener('keyup', (e) => {
      this.keys[e.key.toLowerCase()] = false;
    });

    // Mouse weapon triggers
    window.addEventListener('mousedown', (e) => {
      this.sound.init();
      if (this.state !== GameState.PLAYING) return;
      if (e.target.closest('#game-hud button, .touch-controls')) return;

      if (e.button === 0) this.mouse.beam = true;
      if (e.button === 2) {
        e.preventDefault();
        this.launchSeed();
      }
    });

    window.addEventListener('mouseup', (e) => {
      if (e.button === 0) this.mouse.beam = false;
    });

    window.addEventListener('contextmenu', (e) => e.preventDefault());

    // UI Buttons
    document.getElementById('btn-play').addEventListener('click', () => {
      this.sound.init();
      this.openOverlay(this.dom.overlayLevels);
    });

    document.getElementById('btn-options').addEventListener('click', () => {
      this.sound.init();
      this.openOverlay(this.dom.overlayOptions);
    });

    document.getElementById('btn-credits').addEventListener('click', () => {
      this.openOverlay(this.dom.overlayCredits);
    });

    document.getElementById('btn-tutorial-menu').addEventListener('click', () => {
      this.startTutorial();
    });

    document.getElementById('btn-start-mission').addEventListener('click', () => {
      this.startGame();
    });

    document.getElementById('btn-levels-back').addEventListener('click', () => {
      this.closeOverlays();
    });

    document.getElementById('btn-options-back').addEventListener('click', () => {
      this.closeOverlays();
    });

    document.getElementById('btn-credits-back').addEventListener('click', () => {
      this.closeOverlays();
    });

    // Difficulty selection
    document.querySelectorAll('.difficulty-btn').forEach((btn) => {
      btn.addEventListener('click', (e) => {
        document.querySelectorAll('.difficulty-btn').forEach(b => b.classList.remove('active'));
        btn.classList.add('active');
        this.currentDifficulty = btn.dataset.diff;
      });
    });

    // Sliders
    const musicSlider = document.getElementById('slider-music');
    musicSlider.addEventListener('input', (e) => {
      this.sound.musicVol = e.target.value / 100;
      document.getElementById('label-music-val').innerText = `${e.target.value}%`;
    });

    const sfxSlider = document.getElementById('slider-sfx');
    sfxSlider.addEventListener('input', (e) => {
      this.sound.sfxVol = e.target.value / 100;
      document.getElementById('label-sfx-val').innerText = `${e.target.value}%`;
    });

    // Fullscreen toggle
    document.getElementById('toggle-fullscreen').addEventListener('change', () => {
      this.toggleFullscreen();
    });

    // Pause menu controls
    this.dom.camToggle.addEventListener('click', () => this.toggleCameraMode());
    document.getElementById('btn-pause-toggle').addEventListener('click', () => this.togglePause());
    document.getElementById('btn-resume').addEventListener('click', () => this.resumeGame());
    document.getElementById('btn-retry').addEventListener('click', () => this.startGame());
    document.getElementById('btn-pause-menu').addEventListener('click', () => this.exitToMenu());
    document.getElementById('btn-pause-options').addEventListener('click', () => {
      this.openOverlay(this.dom.overlayOptions);
    });

    // GameOver controls
    document.getElementById('btn-gameover-replay').addEventListener('click', () => this.startGame());
    document.getElementById('btn-gameover-menu').addEventListener('click', () => this.exitToMenu());

    // Tutorial controls
    this.tutIndex = 0;
    this.tutSteps = [
      { step: 'STEP 1 OF 5', title: 'Hello, Pilot!', body: 'The city is engulfed in toxic smog. Your drone Verdant-1 is equipped to purify the air and reforest the city before time runs out.' },
      { step: 'STEP 2 OF 5', title: 'Flight Controls', body: 'Use W/S to move forward/backward, A/D to steer, Space to ascend, and Shift to descend smoothly.' },
      { step: 'STEP 3 OF 5', title: 'Purifier Beam', body: 'Hold Left Click to fire the blue high-energy Purifier Beam. It locks onto pollution clouds and neutralizes them.' },
      { step: 'STEP 4 OF 5', title: 'Instant Seeds', body: 'Aim at glowing green planters and Right Click to shoot Instant Seed capsules. Trees sprout instantly and clean air continuously!' },
      { step: 'STEP 5 OF 5', title: 'Victory Objective', body: 'Reduce city smog to 0% before the clock runs out or before smog saturates to 100%. Watch out for skyscraper collisions!' }
    ];

    document.getElementById('btn-tut-next').addEventListener('click', () => {
      this.tutIndex++;
      if (this.tutIndex >= this.tutSteps.length) {
        this.closeOverlays();
        this.startGame();
      } else {
        this.renderTutorialCard();
      }
    });

    document.getElementById('btn-tut-skip').addEventListener('click', () => {
      this.closeOverlays();
      this.startGame();
    });

    // Touch button bindings for mobile/touch test
    const btnTouchBeam = document.getElementById('btn-touch-beam');
    btnTouchBeam.addEventListener('pointerdown', () => { this.virtualInput.beam = true; });
    window.addEventListener('pointerup', () => { this.virtualInput.beam = false; });

    document.getElementById('btn-touch-seed').addEventListener('click', () => { this.launchSeed(); });
    document.getElementById('btn-touch-up').addEventListener('pointerdown', () => { this.virtualInput.climb = 1; });
    document.getElementById('btn-touch-up').addEventListener('pointerup', () => { this.virtualInput.climb = 0; });
    document.getElementById('btn-touch-down').addEventListener('pointerdown', () => { this.virtualInput.climb = -1; });
    document.getElementById('btn-touch-down').addEventListener('pointerup', () => { this.virtualInput.climb = 0; });
  }

  onResize() {
    this.camera.aspect = window.innerWidth / window.innerHeight;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(window.innerWidth, window.innerHeight);
  }

  toggleFullscreen() {
    if (!document.fullscreenElement) {
      document.documentElement.requestFullscreen().catch(() => {});
    } else {
      document.exitFullscreen().catch(() => {});
    }
  }

  openOverlay(overlay) {
    this.closeOverlays();
    overlay.classList.add('active');
  }

  closeOverlays() {
    document.querySelectorAll('.overlay').forEach(el => el.classList.remove('active'));
  }

  startTutorial() {
    this.tutIndex = 0;
    this.renderTutorialCard();
    this.openOverlay(this.dom.overlayTutorial);
  }

  renderTutorialCard() {
    const s = this.tutSteps[this.tutIndex];
    document.getElementById('tut-step').innerText = s.step;
    document.getElementById('tut-title').innerText = s.title;
    document.getElementById('tut-body').innerText = s.body;
  }

  startGame() {
    this.closeOverlays();
    this.dom.mainMenu.style.display = 'none';
    this.dom.gameHud.classList.add('active');

    const diff = Difficulties[this.currentDifficulty] || Difficulties.NORMAL;
    this.smog = 0.6;
    this.timeRemaining = diff.timeLimit;
    this.score = 0;
    this.cloudsPurified = 0;
    this.treesPlanted = 0;
    this.smogRemoved = 0;
    this.smogRiseTimer = 0;
    this.hasCrashed = false;

    // Reset drone
    this.drone.position.set(0, 10, 0);
    this.velocity.set(0, 0, 0);
    this.droneAltitude = 10;
    this.droneYaw = 0;

    this.state = GameState.PLAYING;
    this.showVoiceToast("Let's clean the air of this city!", 3000);
  }

  togglePause() {
    if (this.state === GameState.PLAYING) {
      this.state = GameState.PAUSED;
      this.openOverlay(this.dom.overlayPause);
    } else if (this.state === GameState.PAUSED) {
      this.resumeGame();
    }
  }

  resumeGame() {
    this.closeOverlays();
    this.state = GameState.PLAYING;
  }

  exitToMenu() {
    this.closeOverlays();
    this.state = GameState.MENU;
    this.dom.gameHud.classList.remove('active');
    this.dom.mainMenu.style.display = 'flex';
  }

  toggleCameraMode() {
    this.isFirstPerson = !this.isFirstPerson;
    this.dom.camToggle.classList.toggle('camera-button--first-person', this.isFirstPerson);
    this.dom.camToggle.classList.toggle('active', this.isFirstPerson);
  }

  showVoiceToast(text, duration = 2800) {
    this.dom.voiceLine.innerText = text;
    this.dom.voiceLine.classList.add('active');
    setTimeout(() => {
      this.dom.voiceLine.classList.remove('active');
    }, duration);
  }

  launchSeed() {
    if (this.state !== GameState.PLAYING || this.hasCrashed) return;

    this.sound.playSeedLaunch();

    // Find closest free planting spot in front
    const dronePos = this.drone.position;
    const forward = new THREE.Vector3(0, 0, 1).applyAxisAngle(new THREE.Vector3(0, 1, 0), this.droneYaw);

    let targetSpot = null;
    let minDistance = 45;

    this.plantingSpots.forEach(s => {
      if (s.isPlanted) return;
      const toSpot = new THREE.Vector3().subVectors(s.position, dronePos);
      const dist = toSpot.length();
      if (dist < minDistance && toSpot.dot(forward) > 0) {
        minDistance = dist;
        targetSpot = s;
      }
    });

    const start = dronePos.clone().add(this.seedMuzzle.clone().applyAxisAngle(new THREE.Vector3(0, 1, 0), this.droneYaw));
    const target = targetSpot ? targetSpot.position.clone() : start.clone().add(forward.clone().multiplyScalar(20)).setY(0);

    // Create projectile
    const seedMesh = new THREE.Mesh(
      new THREE.SphereGeometry(0.35, 8, 8),
      new THREE.MeshBasicMaterial({ color: 0x78ffbe })
    );
    seedMesh.position.copy(start);
    this.scene.add(seedMesh);

    this.seedProjectiles.push({
      mesh: seedMesh,
      start,
      target,
      progress: 0,
      targetSpot
    });
  }

  triggerBloom(spot) {
    if (spot.isPlanted) return;
    spot.isPlanted = true;
    spot.beacon.visible = false;
    spot.haloMat.opacity = 0;

    this.treesPlanted++;
    this.score += 250;
    this.sound.playBloom();

    // Grow tree model
    const treeGroup = new THREE.Group();
    const trunkGeo = new THREE.CylinderGeometry(0.3, 0.45, 3.2, 8);
    const trunkMat = new THREE.MeshStandardMaterial({ color: 0x5c4033, roughness: 0.9 });
    const trunk = new THREE.Mesh(trunkGeo, trunkMat);
    trunk.position.y = 1.6;
    treeGroup.add(trunk);

    const foliageGeo = new THREE.DodecahedronGeometry(2.0, 1);
    const foliageMat = new THREE.MeshStandardMaterial({ color: 0x22c55e, roughness: 0.5 });
    const foliage = new THREE.Mesh(foliageGeo, foliageMat);
    foliage.position.y = 3.6;
    treeGroup.add(foliage);

    treeGroup.position.copy(spot.position);
    treeGroup.scale.set(0.1, 0.1, 0.1);
    this.scene.add(treeGroup);

    // Bounce scale animation
    let t = 0;
    const growInterval = setInterval(() => {
      t += 0.05;
      const s = Math.min(1.0, t) * (1.0 + 0.2 * Math.sin(t * Math.PI));
      treeGroup.scale.setScalar(s);
      if (t >= 1.0) clearInterval(growInterval);
    }, 20);

    // Passive air clean reduction
    this.reduceSmog(0.04);
  }

  reduceSmog(amount) {
    const before = this.smog;
    this.smog = Math.max(0, this.smog - amount);
    this.smogRemoved += (before - this.smog);
    if (this.smog <= 0 && this.state === GameState.PLAYING) {
      this.endGame('VICTORY');
    }
  }

  endGame(result) {
    this.state = GameState.GAMEOVER;
    this.sound.setBeamActive(false);

    const isWin = result === 'VICTORY';
    if (isWin) {
      this.sound.playVictory();
      const timeBonus = Math.ceil(this.timeRemaining) * 15;
      this.score += timeBonus;
      this.dom.gameoverTitle.innerText = 'CLEAN AIR!';
      this.dom.gameoverSubtitle.innerText = `You cleansed the city smog with ${this.formatTime(this.timeRemaining)} to spare!`;
      this.dom.gameoverPanel.classList.remove('gameover--defeat');
      this.dom.gameoverPanel.classList.add('gameover--victory');
      this.showVoiceToast('Look at the blue sky!', 4000);
    } else {
      this.sound.playCrash();
      this.dom.gameoverTitle.innerText = result === 'CRASH' ? 'YOU CRASHED!' : (result === 'TIMEUP' ? "TIME'S UP!" : 'POLLUTION SATURATION!');
      this.dom.gameoverSubtitle.innerText = result === 'CRASH' ? 'The drone collided with a building facade.' : (result === 'TIMEUP' ? 'Air purification timer expired.' : 'Smog density reached toxic 100% capacity.');
      this.dom.gameoverPanel.classList.remove('gameover--victory');
      this.dom.gameoverPanel.classList.add('gameover--defeat');
    }

    this.dom.statScore.innerText = this.score.toLocaleString();
    this.dom.statSmog.innerText = `${Math.round(this.smogRemoved * 100)}%`;
    this.dom.statClouds.innerText = this.cloudsPurified;
    this.dom.statTrees.innerText = `${this.treesPlanted}/${this.totalPlantingSpots}`;

    this.openOverlay(this.dom.overlayGameOver);
  }

  formatTime(seconds) {
    const m = Math.floor(seconds / 60);
    const s = Math.floor(seconds % 60);
    return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
  }

  // --- Main Physics & Frame Update ---
  update(dt) {
    // Propeller spinning animation
    this.rotors.forEach(r => r.rotation.y += 35 * dt);

    if (this.state !== GameState.PLAYING) return;

    // Timer countdown
    this.timeRemaining -= dt;
    if (this.timeRemaining <= 0) {
      this.endGame('TIMEUP');
      return;
    }

    // Passive smog rising per interval
    const diff = Difficulties[this.currentDifficulty] || Difficulties.NORMAL;
    this.smogRiseTimer += dt;
    if (this.smogRiseTimer >= diff.smogInterval) {
      this.smogRiseTimer -= diff.smogInterval;
      this.smog = Math.min(1.0, this.smog + 0.01);
      if (this.smog >= 1.0) {
        this.endGame('SATURATION');
        return;
      }
    }

    // Passive reduction from planted trees
    if (this.treesPlanted > 0) {
      this.reduceSmog(0.002 * this.treesPlanted * dt);
    }

    // Controls input gathering
    let inputForward = 0;
    let inputSteer = 0;
    let inputClimb = this.virtualInput.climb;

    if (this.keys['w'] || this.keys['arrowup']) inputForward += 1;
    if (this.keys['s'] || this.keys['arrowdown']) inputForward -= 1;
    if (this.keys['a'] || this.keys['arrowleft']) inputSteer -= 1;
    if (this.keys['d'] || this.keys['arrowright']) inputSteer += 1;
    if (this.keys[' ']) inputClimb += 1;
    if (this.keys['shift']) inputClimb -= 1;

    // Heading Yaw steering
    this.droneYaw -= inputSteer * 2.2 * dt;

    // Movement calculation
    const maxSpeed = 12;
    const forwardVec = new THREE.Vector3(0, 0, 1).applyAxisAngle(new THREE.Vector3(0, 1, 0), this.droneYaw);
    const desiredVel = forwardVec.clone().multiplyScalar(inputForward * maxSpeed);

    this.velocity.lerp(desiredVel, 6 * dt);
    this.drone.position.addScaledVector(this.velocity, dt);

    // Altitude calculation
    this.droneAltitude = Math.max(1.0, Math.min(55.0, this.droneAltitude + inputClimb * 6 * dt));
    this.drone.position.y += (this.droneAltitude - this.drone.position.y) * 8 * dt;

    // Drone visual tilt banking
    const forwardSpeed = this.velocity.dot(forwardVec);
    this.droneTiltX = THREE.MathUtils.lerp(this.droneTiltX, (inputForward > 0 ? 0.25 : (inputForward < 0 ? -0.2 : 0)), 10 * dt);
    this.droneTiltZ = THREE.MathUtils.lerp(this.droneTiltZ, -inputSteer * 0.35, 10 * dt);

    this.drone.rotation.set(this.droneTiltX, this.droneYaw, this.droneTiltZ, 'YXZ');

    // Engine Audio update
    this.sound.updateDronePitch(this.velocity.length(), inputClimb, true);

    // Boundary check
    const distFromOrigin = Math.max(Math.abs(this.drone.position.x), Math.abs(this.drone.position.z));
    this.dom.boundary.classList.toggle('active', distFromOrigin > this.playAreaHalf - 6);
    if (distFromOrigin > this.playAreaHalf) {
      this.drone.position.x = Math.max(-this.playAreaHalf, Math.min(this.playAreaHalf, this.drone.position.x));
      this.drone.position.z = Math.max(-this.playAreaHalf, Math.min(this.playAreaHalf, this.drone.position.z));
    }

    // Building Collision Hazard Detection
    for (const b of this.buildings) {
      if (this.drone.position.x > b.minX - 0.8 && this.drone.position.x < b.maxX + 0.8 &&
          this.drone.position.z > b.minZ - 0.8 && this.drone.position.z < b.maxZ + 0.8) {
        if (this.drone.position.y < b.height - 0.3) {
          this.hasCrashed = true;
          this.endGame('CRASH');
          return;
        }
      }
    }

    // Target cloud lock-on detection
    this.updateTargeting();

    // Beam firing update
    const isBeamFiring = (this.mouse.beam || this.virtualInput.beam) && !this.hasCrashed;
    this.sound.setBeamActive(isBeamFiring);
    this.updateBeam(isBeamFiring);

    // Projectile tracking
    this.updateProjectiles(dt);

    // Wandering clouds
    this.smogClouds.forEach(c => {
      c.group.position.x += c.wanderX * dt;
      c.group.position.z += c.wanderZ * dt;
    });

    // Atmosphere color grading following smog level
    const cleanSky = new THREE.Color(0x38bdf8);
    const smogSky = new THREE.Color(0x1a2e3b);
    this.scene.background.lerpColors(cleanSky, smogSky, this.smog);
    this.scene.fog.density = 0.005 + this.smog * 0.02;

    // HUD element updates
    this.updateHUD();
  }

  updateTargeting() {
    const dronePos = this.drone.position;
    const forward = new THREE.Vector3(0, 0, 1).applyAxisAngle(new THREE.Vector3(0, 1, 0), this.droneYaw);

    let bestCloud = null;
    let minCloudDist = 40;

    this.smogClouds.forEach(c => {
      const toCloud = new THREE.Vector3().subVectors(c.group.position, dronePos);
      const dist = toCloud.length();
      if (dist < minCloudDist) {
        const dot = toCloud.normalize().dot(forward);
        if (dot > 0.85) {
          minCloudDist = dist;
          bestCloud = c;
        }
      }
    });

    this.activeTargetCloud = bestCloud;
    this.dom.crosshair.classList.toggle('locked', bestCloud !== null);

    // Highlight nearest planting spot
    let bestSpot = null;
    let minSpotDist = 35;
    this.plantingSpots.forEach(s => {
      if (s.isPlanted) {
        s.haloMat.opacity = 0;
        return;
      }
      const toSpot = new THREE.Vector3().subVectors(s.position, dronePos);
      const dist = toSpot.length();
      if (dist < minSpotDist) {
        const dot = toSpot.normalize().dot(forward);
        if (dot > 0.75) {
          minSpotDist = dist;
          bestSpot = s;
        }
      }
      s.haloMat.opacity = (s === bestSpot) ? 0.9 : 0;
    });
    this.activeTargetSpot = bestSpot;
  }

  updateBeam(isFiring) {
    if (!isFiring) {
      this.beamMat.opacity = 0;
      return;
    }

    const muzzleWorld = this.drone.position.clone().add(this.beamMuzzle.clone().applyAxisAngle(new THREE.Vector3(0, 1, 0), this.droneYaw));
    const forward = new THREE.Vector3(0, 0, 1).applyAxisAngle(new THREE.Vector3(0, 1, 0), this.droneYaw);

    let endPoint = muzzleWorld.clone().add(forward.clone().multiplyScalar(35));

    if (this.activeTargetCloud) {
      endPoint = this.activeTargetCloud.group.position.clone();

      // Purify cloud
      this.sound.playPop();
      this.scene.remove(this.activeTargetCloud.group);
      const idx = this.smogClouds.indexOf(this.activeTargetCloud);
      if (idx !== -1) this.smogClouds.splice(idx, 1);

      this.cloudsPurified++;
      this.score += 100;
      this.reduceSmog(0.05);
      this.activeTargetCloud = null;
    }

    // Orient and scale beam mesh
    const beamVector = new THREE.Vector3().subVectors(endPoint, muzzleWorld);
    const dist = beamVector.length();

    this.beamMesh.position.copy(muzzleWorld).addScaledVector(beamVector, 0.5);
    this.beamMesh.scale.set(1, 1, dist);
    this.beamMesh.lookAt(endPoint);
    this.beamMat.opacity = 0.85;
  }

  updateProjectiles(dt) {
    for (let i = this.seedProjectiles.length - 1; i >= 0; i--) {
      const p = this.seedProjectiles[i];
      p.progress += dt * 1.3;

      if (p.progress >= 1.0) {
        this.scene.remove(p.mesh);
        this.seedProjectiles.splice(i, 1);
        if (p.targetSpot && !p.targetSpot.isPlanted) {
          this.triggerBloom(p.targetSpot);
        }
      } else {
        // Parabolic arc
        p.mesh.position.lerpVectors(p.start, p.target, p.progress);
        p.mesh.position.y += Math.sin(p.progress * Math.PI) * 7;
      }
    }
  }

  updateHUD() {
    const pct = Math.round(this.smog * 100);
    this.dom.smogVal.innerText = `${pct}%`;
    this.dom.smogFill.style.width = `${pct}%`;
    if (this.smog > 0.65) {
      this.dom.smogFill.style.backgroundColor = 'rgb(220, 90, 80)';
    } else if (this.smog > 0.35) {
      this.dom.smogFill.style.backgroundColor = 'rgb(200, 150, 80)';
    } else {
      this.dom.smogFill.style.backgroundColor = 'rgb(120, 230, 170)';
    }

    this.dom.timer.innerText = this.formatTime(this.timeRemaining);
    this.dom.timer.classList.toggle('hud-timer--warning', this.timeRemaining < 30);

    this.dom.score.innerText = this.score.toLocaleString();
    this.dom.trees.innerText = `TREES ${this.treesPlanted}/${this.totalPlantingSpots}`;
  }

  updateCamera() {
    if (this.state === GameState.MENU) {
      // Cinematic menu orbit
      const t = performance.now() * 0.00025;
      this.camera.position.x = Math.sin(t) * 45;
      this.camera.position.z = Math.cos(t) * 45;
      this.camera.position.y = 20;
      this.camera.lookAt(0, 8, 0);
      return;
    }

    const dronePos = this.drone.position;
    const forward = new THREE.Vector3(0, 0, 1).applyAxisAngle(new THREE.Vector3(0, 1, 0), this.droneYaw);

    if (this.isFirstPerson) {
      // FPV Cockpit camera mounted between front propellers
      const cockpitMount = dronePos.clone().add(forward.clone().multiplyScalar(0.7)).add(new THREE.Vector3(0, 0.35, 0));
      this.camera.position.copy(cockpitMount);
      const lookTarget = cockpitMount.clone().add(forward.clone().multiplyScalar(30)).add(new THREE.Vector3(0, -1.2, 0));
      this.camera.lookAt(lookTarget);
    } else {
      // 3rd Person Follow Camera
      const desiredPos = dronePos.clone().sub(forward.clone().multiplyScalar(6.5)).add(new THREE.Vector3(0, 3.2, 0));
      this.camera.position.lerp(desiredPos, 0.15);
      const lookTarget = dronePos.clone().add(forward.clone().multiplyScalar(10)).add(new THREE.Vector3(0, -0.5, 0));
      this.camera.lookAt(lookTarget);
    }
  }

  render(time) {
    const dt = Math.min(0.1, (time - this.lastTime) / 1000);
    this.lastTime = time;

    this.update(dt);
    this.updateCamera();
    this.renderer.render(this.scene, this.camera);

    requestAnimationFrame(this.render.bind(this));
  }
}

// Instantiate game instance on load
window.addEventListener('DOMContentLoaded', () => {
  window.game = new VerdantZeroGame();
});
