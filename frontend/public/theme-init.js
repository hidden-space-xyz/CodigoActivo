try {
  if (localStorage.getItem('ca-theme') === 'dark') {
    document.documentElement.classList.add('ca-dark')
  }
} catch {}
