'use strict';
const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const root=path.join(__dirname,'..','PhotoSafeCheck');
const read=f=>fs.readFileSync(path.join(root,f),'utf8');
test('first run can choose either portable or local mode',()=>{
 const app=read('App.cs'); const window=read('StorageModeWindow.cs');
 assert.match(app,/TryReadPreference/); assert.match(app,/new StorageModeWindow/);
 assert.match(window,/GroupName = "mode"/); assert.match(window,/Portable — keep everything/);
 assert.match(window,/Local — store data/); assert.match(window,/Remember my choice/);
});
test('no logs written until storage is chosen',()=>{
 const diag=read('StartupDiagnostics.cs');
 assert.match(diag,/if \(!StoragePaths\.IsSelected\) \{ Pending\.Add\(line\); return; \}/);
 assert.match(diag,/FlushPending\(\)/);
 assert.doesNotMatch(read('Program.cs'),/Directory\.CreateDirectory/);
});
test('portable data is next to executable and local data in OS profile',()=>{
 const p=read('StoragePaths.cs');
 assert.match(p,/AppContext\.BaseDirectory, "Data"/);
 assert.match(p,/Environment\.SpecialFolder\.LocalApplicationData/);
 assert.match(p,/"storage-choice\.json"/);
 assert.match(p,/"state\.json", "pairing-token\.txt"/);
 assert.match(p,/File\.Copy\(from, to, overwrite: false\)/);
});
test('storage switching is user-initiated and only applies on relaunch',()=>{
 const ui=read('MainWindow.cs');
 assert.match(ui,/Storage mode and cleanup/);
 assert.match(ui,/AskOnNextLaunch\(\)/);
 assert.match(ui,/On the next launch, choose whether to carry over/);
});
test('cleanup only touches app-owned whitelisted files and never indexed photos',()=>{
 const code=read('AppDataCleanup.cs');
 for(const name of ['state.json','pairing-token.txt','storage-choice.json','startup.log']) assert.ok(code.includes(name));
 assert.match(code,/if \(!StoragePaths\.IsPortable\)/);
 assert.doesNotMatch(code,/Delete\(.*PhotoRecord|EnumerateFiles\(.*AllDirectories|Delete\(StoragePaths\.DataDirectory/);
 assert.match(code,/ReparsePoint/);
});
