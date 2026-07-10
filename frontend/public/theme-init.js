// Pre-paint theme bootstrap. Kept as an external file (not inline in
// index.html) so the CSP can stay `script-src 'self'` without hashes.
try {
  if (localStorage.getItem('ca-theme') === 'dark') {
    document.documentElement.classList.add('ca-dark')
  }
} catch {}
