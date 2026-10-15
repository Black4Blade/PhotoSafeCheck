const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const project = path.join(__dirname, '..', 'PhotoSafeCheck');
const read = (file) => fs.readFileSync(path.join(project, file), 'utf8');
test('never touch Avalonia UI dispatcher before app initialization', () => {
  const program = read('Program.cs');
  assert.doesNotMatch(program, /Dispatcher\.UIThread\.(UnhandledException|ShutdownStarted)\s*\+=/);
  assert.match(read('App.cs'), /OnFrameworkInitializationCompleted\(\)/);
  assert.match(read('App.cs'), /Dispatcher\.UIThread\.UnhandledException\s*\+=/);
});
test('portable settings and logs share the same data directory', () => {
  assert.match(read('StoragePaths.cs'), /portable\.mode/);
  assert.match(read('Models.cs'), /StoragePaths\.DataDirectory/);
  assert.match(read('StartupDiagnostics.cs'), /StoragePaths\.DataDirectory/);
});
test('no Playwright dependency is reintroduced', () => {
  assert.doesNotMatch(read('PhotoSafeCheck.csproj'), /PackageReference Include="Microsoft\.Playwright"/);
});
test('distribution only stages an executable without any private state', () => {
  const packager = fs.readFileSync(path.join(__dirname, '..', 'scripts', 'package_preview.py'), 'utf8');
  assert.match(packager, /shutil\.copy2\(source, target\)/);
  assert.doesNotMatch(packager, /shutil\.copytree/);
  assert.doesNotMatch(packager, /portable\.mode/);
});
