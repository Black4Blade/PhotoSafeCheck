 'use strict';
const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const vm=require('node:vm');
const manifest=require('../Extension/manifest.json');

test('least privilege manifest retains necessary scoped permissions',()=>{
  assert.equal(manifest.manifest_version,3);
  assert.equal(manifest.version,'0.3.5');
  assert.deepEqual(manifest.permissions,['storage']);
  assert.deepEqual(manifest.host_permissions,['https://photos.google.com/*','http://127.0.0.1:37541/*']);
  assert.deepEqual(manifest.content_scripts[0].matches,['https://photos.google.com/*']);
});

async function popupWithTab(url) {
 const elements={};
 const getElementById=id=>elements[id]??(elements[id]={id,disabled:false,hidden:false,value:'',textContent:'',dataset:{},addEventListener(type,cb){this['on'+type]=cb;}});
 const document={getElementById};
 let sent=[];
 const chrome={
  tabs:{query:async()=>[{id:22,url}],sendMessage:async(id,msg)=>{sent.push([id,msg]);return {ok:true,data:{tileCount:0}};}},
  runtime:{sendMessage:(message,callback)=>callback({ok:true,data:{totalFiles:2,notChecked:2,remainingSearches:2,dailyUsed:0,delaySeconds:12,enabled:false,estimatedSecondsRemaining:40,message:'Ready'}})},
  storage:{local:{get:async()=>({token:'a'.repeat(48),enabled:false,activeTabId:null}),set:async()=>{}}}
 };
 const sandbox={chrome,document,Date,Math,Number,Promise,String,JSON,navigator:{clipboard:{writeText:async()=>{}}},setInterval:()=>{},console};
 vm.runInNewContext(fs.readFileSync('Extension/popup.js','utf8'),sandbox,{filename:'popup.js'});
 await new Promise(resolve=>setImmediate(resolve));
 return {elements,sent};
}

test('Chrome popup detects Google Photos using site-scoped tab.url',async()=>{
 const {elements}=await popupWithTab('https://photos.google.com/search/example');
 assert.equal(elements.activate.disabled,false);
 assert.equal(elements.diagnostics.disabled,false);
});
test('Chrome popup does not activate on unrelated websites',async()=>{
 const {elements}=await popupWithTab('https://example.com/');
 assert.equal(elements.activate.disabled,true);
 assert.equal(elements.diagnostics.disabled,true);
});

test('service worker validates origin from sender.tab without broad tabs grant',async()=>{
 let listener;
 const token='a'.repeat(48);
 const chrome={runtime:{onMessage:{addListener:fn=>{listener=fn;}}},storage:{local:{get:async() => ({token,enabled:true,activeTabId:22})}}};
 const sandbox={chrome, fetch:async()=>({ok:true,status:200,json:async()=>({state:'waiting'})}), AbortSignal,console};
 vm.runInNewContext(fs.readFileSync('Extension/service_worker.js','utf8'),sandbox,{filename:'service_worker.js'});
 const send=(url)=>new Promise(resolve=>listener({type:'JOB',client:'valid-client'},{tab:{id:22,url}},resolve));
 const ok=await send('https://photos.google.com/search/foo');
 assert.equal(ok.ok,true);
 const bad=await send('https://example.com');
 assert.equal(bad.ok,false);
 assert.match(bad.error,/Only the selected Google Photos tab/);
});
