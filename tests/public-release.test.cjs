'use strict';
const fs=require('node:fs');
const path=require('node:path');
const test=require('node:test');
const assert=require('node:assert/strict');
const repo=path.resolve(__dirname,'..');
const script=fs.readFileSync(path.join(repo,'scripts/package-itcoder-qa-release.ps1'),'utf8');
const verifier=fs.readFileSync(path.join(repo,'scripts/verify-windows-portable-nongui.ps1'),'utf8');
const project=fs.readFileSync(path.join(repo,'windows-itcoder-qa/ITCoderStudyQA.csproj'),'utf8');
test('released independent project remains source-only and identifies itself accurately',()=>{
 assert.match(project,/<Version>0\.5\.9<\/Version>/);
 assert.match(project,/independent/i);
 assert.match(fs.readFileSync(path.join(repo,'README.md'),'utf8'),/not.*official HUTECH/i);
});
test('release has integrity manifest, safe extraction and offline compiled verification',()=>{
 for(const token of ['Get-FileHash -LiteralPath $target -Algorithm SHA256','independentNotOfficialExamClient = $true','Refusing existing release output directory','Symlink/reparse point in published release','verify-windows-portable-nongui.ps1'])assert.ok(script.includes(token),token);
 for(const token of ['Get-FileHash -LiteralPath $full -Algorithm SHA256','CHECKSUMS.json','ui-session0-preflight','compiledPoliciesPassed=13','guiVerified=$false'])assert.ok(verifier.includes(token),token);
});
test('public checkout does not contain vendor installer artifacts',()=>{
 for(const p of ['vendor','decompiled','recovered','original-app','credentials'])assert.equal(fs.existsSync(path.join(repo,p)),false,p);
});
