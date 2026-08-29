const { execSync } = require('child_process');
const fs = require('fs');
const path = require('path');

function run(label, cmd, cwd) {
  try {
    console.log(`\n[テスト:${label}] 実行中...`);
    execSync(cmd, { stdio: 'inherit', shell: true, cwd });
    console.log(`[テスト:${label}] 通過`);
  } catch {
    console.error(`[テスト:${label}] 失敗あり。確認してください。`);
  }
}

if (fs.existsSync(path.join(process.cwd(), 'frontend', 'package.json'))) {
  run('frontend', 'npm run test', path.join(process.cwd(), 'frontend'));
}
if (fs.existsSync(path.join(process.cwd(), 'backend'))) {
  run('backend', 'dotnet test backend', process.cwd());
}
if (fs.existsSync(path.join(process.cwd(), 'collector'))) {
  run('collector', 'pytest collector', process.cwd());
}
