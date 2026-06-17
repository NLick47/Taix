const fs = require('fs');
const path = require('path');

const browser = process.argv[2] || 'chrome';
const validBrowsers = ['chrome', 'firefox', 'safari'];

if (!validBrowsers.includes(browser)) {
  console.error(`Invalid browser: ${browser}. Valid options: ${validBrowsers.join(', ')}`);
  process.exit(1);
}

const rootDir = path.join(__dirname, '..');
const srcDir = path.join(rootDir, 'src');
const distDir = path.join(rootDir, 'dist', browser);
const iconsDir = path.join(rootDir, 'icons');
const manifestSrc = path.join(rootDir, 'manifests', `manifest.${browser}.json`);

if (!fs.existsSync(manifestSrc)) {
  console.error(`Manifest file not found: ${manifestSrc}`);
  process.exit(1);
}

fs.mkdirSync(distDir, { recursive: true });
fs.mkdirSync(path.join(distDir, 'icons'), { recursive: true });

const esbuild = require('esbuild');

try {
  esbuild.buildSync({
    entryPoints: [path.join(srcDir, 'background.ts')],
    bundle: true,
    outfile: path.join(distDir, 'background.js'),
    format: 'iife',
    platform: 'browser',
    target: 'es2020',
    minify: false,
  });

  esbuild.buildSync({
    entryPoints: [path.join(srcDir, 'content.ts')],
    bundle: true,
    outfile: path.join(distDir, 'content.js'),
    format: 'iife',
    platform: 'browser',
    target: 'es2020',
    minify: false,
  });
} catch (err) {
  console.error('Build failed:', err);
  process.exit(1);
}

fs.copyFileSync(manifestSrc, path.join(distDir, 'manifest.json'));

if (fs.existsSync(iconsDir)) {
  const icons = fs.readdirSync(iconsDir);
  for (const icon of icons) {
    fs.copyFileSync(path.join(iconsDir, icon), path.join(distDir, 'icons', icon));
  }
}

console.log(`Build complete: ${browser}`);