try {
  if (localStorage.getItem('ca-theme') === 'dark') {
    document.documentElement.classList.add('ca-dark', 'dark')
    var meta = document.querySelector('meta[name="theme-color"]')
    if (meta) meta.setAttribute('content', '#101014')
  }
} catch {}
