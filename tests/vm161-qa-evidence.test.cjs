'use strict';
const fs=require('node:fs');
const path=require('node:path');
const test=require('node:test');
const assert=require('node:assert/strict');
const root=path.resolve(__dirname,'..');
const report=JSON.parse(fs.readFileSync(path.join(root,'docs/VM161-QA-20261115.json'),'utf8'));
const notes=fs.readFileSync(path.join(root,'docs/VM161-QA-20261115.md'),'utf8');

test('VM161 archived ZIP hash and executable provenance are pinned',()=>{
 assert.equal(report.subject.includes('Independent Windows 11 VM'),true);
 assert.match(report.originalGithubZipSha256,/^[A-F0-9]{64}$/);
 assert.equal(report.guestComputedZipSha256,report.originalGithubZipSha256);
 assert.match(report.exeSHA256,/^[A-F0-9]{64}$/);
});
test('remote offline QA report is structurally consistent and all expected gates pass',()=>{
 assert.equal(report.package.manifestFilesChecked,486);
 assert.equal(report.package.extractedFiles,487);
 assert.equal(report.package.compiledPolicyChecksPassed,13);
 assert.equal(report.package.offlineExecutionProbesPassed,6);
 assert.equal(report.package.failures,0);
 assert.equal(report.runtime.webView2Present,true);
 assert.equal(report.cliExitCodes['ui-preflight'],40);
 assert.equal(report.cliExitCodes['offline-ui-smoke'],40);
 assert.equal(report.cliExitCodes['self-test'],0);
 assert.equal(report.cliExitCodes['runtime-check'],0);
});
test('evidence does not claim GUI, real login or secure exam parity',()=>{
 assert.equal(report.interaction.guiVerified,false);
 assert.equal(report.interaction.liveSiteLoginTested,false);
 assert.equal(report.interaction.secureExamVendorParityVerified,false);
 assert.equal(report.environmentChange.guestFirewallChanged,false);
 assert.equal(report.environmentChange.win181Session2Touched,false);
 assert.match(notes,/Real interactive window \| NOT TESTED/);
 assert.match(notes,/Website account login \| NOT TESTED/);
});
