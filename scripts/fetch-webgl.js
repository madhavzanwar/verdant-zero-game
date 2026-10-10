const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

const BASE_URL = 'https://raw.githubusercontent.com/elianismedina/smogbustergame/gh-pages';
const TARGET_DIR = path.join(__dirname, '..', 'unity-webgl');

const files = [
  'index.html',
  'Build/WebGL.loader.js',
  'Build/WebGL.framework.js.unityweb',
  'Build/WebGL.data.unityweb',
  'Build/WebGL.wasm.unityweb',
  'TemplateData/style.css',
  'TemplateData/favicon.ico',
  'TemplateData/fullscreen-button.png',
  'TemplateData/progress-bar-empty-dark.png',
  'TemplateData/progress-bar-empty-light.png',
  'TemplateData/progress-bar-full-dark.png',
  'TemplateData/progress-bar-full-light.png',
  'TemplateData/unity-logo-dark.png',
  'TemplateData/unity-logo-light.png',
  'TemplateData/unity-logo-title-footer.png',
  'TemplateData/webmemd-icon.png',
  'TemplateData/MemoryProfiler.png'
];

fs.mkdirSync(path.join(TARGET_DIR, 'Build'), { recursive: true });
fs.mkdirSync(path.join(TARGET_DIR, 'TemplateData'), { recursive: true });

console.log('[WebGL Sync] Downloading authentic Unity WebGL build from smogbustergame/gh-pages...');

for (const relPath of files) {
  const destPath = path.join(TARGET_DIR, relPath);
  const url = `${BASE_URL}/${relPath}`;
  console.log(`[WebGL Sync] Fetching ${relPath}...`);
  try {
    execSync(`curl.exe -fL "${url}" -o "${destPath}"`, { stdio: 'inherit' });
  } catch (err) {
    console.error(`Failed to download ${relPath}:`, err.message);
    process.exit(1);
  }
}

console.log('[WebGL Sync] All Unity WebGL build files successfully downloaded!');
