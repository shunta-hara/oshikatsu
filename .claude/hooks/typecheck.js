const { execSync } = require('child_process');
const fs = require('fs');
const path = require('path');
const chunks = [];
process.stdin.on('data', d => chunks.push(d));
process.stdin.on('end', () => {
  try {
    const data = JSON.parse(Buffer.concat(chunks).toString());
    const filePath = data.tool_input?.file_path || '';

    if (/\.(ts|tsx|mts)$/.test(filePath)) {
      const tsconfig = path.join(process.cwd(), 'frontend', 'tsconfig.json');
      if (!fs.existsSync(tsconfig)) return;
      try {
        execSync('npx tsc --noEmit', { stdio: 'pipe', cwd: path.join(process.cwd(), 'frontend') });
      } catch (e) {
        const out = (e.stdout || e.stderr || '').toString();
        const lines = out.split('\n').filter(Boolean).slice(0, 15);
        if (lines.length) console.error('[frontend 型チェック]\n' + lines.join('\n'));
      }
    } else if (/\.py$/.test(filePath)) {
      try {
        execSync('python -m mypy collector', { stdio: 'pipe' });
      } catch (e) {
        const out = (e.stdout || e.stderr || '').toString();
        const lines = out.split('\n').filter(Boolean).slice(0, 15);
        if (lines.length) console.error('[collector 型チェック]\n' + lines.join('\n'));
      }
    }
  } catch {}
});
