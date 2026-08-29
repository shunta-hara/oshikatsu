const { execSync } = require('child_process');
const fs = require('fs');
const path = require('path');
const chunks = [];
process.stdin.on('data', d => chunks.push(d));
process.stdin.on('end', () => {
  try {
    const data = JSON.parse(Buffer.concat(chunks).toString());
    const command = data.tool_input?.command || '';
    if (!/git\s+commit/.test(command)) return;

    console.log('\n[コミット前チェック] 実行中...');
    let failed = false;

    if (fs.existsSync(path.join(process.cwd(), 'frontend', 'package.json'))) {
      try { execSync('npx tsc --noEmit', { stdio: 'pipe', cwd: path.join(process.cwd(), 'frontend') }); console.log('  frontend tsc OK'); }
      catch (e) { console.error('  frontend tsc FAIL\n' + (e.stdout || '').toString().split('\n').slice(0, 10).join('\n')); failed = true; }
      try { execSync('npm run test', { stdio: 'pipe', cwd: path.join(process.cwd(), 'frontend') }); console.log('  frontend test OK'); }
      catch (e) { console.error('  frontend test FAIL\n' + (e.stdout || '').toString().split('\n').slice(0, 15).join('\n')); failed = true; }
    }

    if (fs.existsSync(path.join(process.cwd(), 'backend'))) {
      try { execSync('dotnet build backend', { stdio: 'pipe' }); console.log('  backend build OK'); }
      catch (e) { console.error('  backend build FAIL\n' + (e.stdout || '').toString().split('\n').slice(0, 15).join('\n')); failed = true; }
      try { execSync('dotnet test backend', { stdio: 'pipe' }); console.log('  backend test OK'); }
      catch (e) { console.error('  backend test FAIL\n' + (e.stdout || '').toString().split('\n').slice(0, 15).join('\n')); failed = true; }
    }

    if (fs.existsSync(path.join(process.cwd(), 'collector'))) {
      try { execSync('pytest collector', { stdio: 'pipe' }); console.log('  collector test OK'); }
      catch (e) { console.error('  collector test FAIL\n' + (e.stdout || '').toString().split('\n').slice(0, 15).join('\n')); failed = true; }
    }

    if (failed) { console.error('\n[コミット前チェック] 修正してからコミットしてください。'); process.exitCode = 1; }
    else { console.log('\n[コミット前チェック] 全チェック通過'); }
  } catch {}
});
