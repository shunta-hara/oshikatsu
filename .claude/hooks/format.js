const { execSync } = require('child_process');
const path = require('path');
const chunks = [];
process.stdin.on('data', d => chunks.push(d));
process.stdin.on('end', () => {
  try {
    const data = JSON.parse(Buffer.concat(chunks).toString());
    const filePath = data.tool_input?.file_path || '';
    if (/\.(ts|tsx|js|jsx|mts|mjs)$/.test(filePath)) {
      try { execSync(`npx prettier --write "${filePath}"`, { stdio: 'pipe', cwd: path.join(process.cwd(), 'frontend') }); } catch {}
      try { execSync(`npx eslint --fix "${filePath}"`, { stdio: 'pipe', cwd: path.join(process.cwd(), 'frontend') }); } catch {}
    } else if (/\.cs$/.test(filePath)) {
      try { execSync('dotnet format backend', { stdio: 'pipe' }); } catch {}
    } else if (/\.py$/.test(filePath)) {
      try { execSync(`python -m black "${filePath}"`, { stdio: 'pipe' }); } catch {}
    }
  } catch {}
});
