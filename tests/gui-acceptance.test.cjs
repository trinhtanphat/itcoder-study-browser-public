'use strict';
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const assert = require('node:assert/strict');
const {spawnSync} = require('node:child_process');
const root = path.resolve(__dirname,'..');
const script = path.join(root,'scripts','test-interactive-gui.ps1');
const txt = fs.readFileSync(script,'utf8');
test('GUI acceptance explicitly rejects Session 2 and non-interactive contexts before launch', () => {
  const guard = txt.indexOf('QA_SESSION_GUARD:');
  const processStart = txt.indexOf('Start-Process -FilePath $exe');
  assert.ok(guard >= 0 && processStart > guard);
  for(const phrase of ['$sessionId -le 0','$sessionId -eq 2','$QaSessionId -eq 2','$sessionId -ne $QaSessionId','[Environment]::UserInteractive'])
    assert.ok(txt.includes(phrase),phrase);
});
test('GUI automation runs pinned independent executable only, with synthetic HTTPS and bounded shutdown', () => {
  for(const phrase of ['ITCoderStudyQA.exe','Get-FileHash -LiteralPath $exe','https://example.org/','UIAutomationClient','AutomationElement','Back','Forward','Reload','Home','Go','CloseMainWindow','WaitForExit(5000)'])
    assert.ok(txt.includes(phrase),phrase);
  assert.doesNotMatch(txt,/--remember-session|https:\/\/itcoder\.hutech\.edu\.vn|password\s*=|sessionStorage|localStorage/);
});
test('unverified exam/browser features never count as GUI pass', () => {
  assert.match(txt,/nativeGuiVerified = \$passed/);
  assert.match(txt,/websiteLoginTested = \$false/);
  assert.match(txt,/officialExamParityVerified = \$false/);
  assert.match(txt,/userSession2Touched = \$false/);
  assert.match(txt,/QA_GUI_ACCEPTANCE_FAILED/);
  assert.match(txt,/Add-Check 'https-navigation-completed' \$navigationObserved/);
  assert.match(txt,/navigationDeadline/);
});
test('Windows Session 0 rejects test before launching any GUI process or creating report', {skip:process.platform!=='win32'}, () => {
  const exe=path.join(root,'nonexistent','ITCoderStudyQA.exe');
  const report=path.join(root,'nonexistent','gui-report.json');
  for(const session of ['1','2']) {
    const result=spawnSync('powershell.exe',['-NoProfile','-NonInteractive','-ExecutionPolicy','Bypass','-File',script,
      '-ExecutablePath',exe,'-QaSessionId',session,'-ReportPath',report],{encoding:'utf8',timeout:15000});
    assert.notEqual(result.status,0);
    assert.match((result.stderr||'')+(result.stdout||''),/QA_SESSION_GUARD/);
  }
  assert.equal(fs.existsSync(report),false);
});
