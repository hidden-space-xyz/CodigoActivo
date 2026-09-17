import { createApp } from 'vue'

import App from '@/app/App.vue'
import { registerProviders } from '@/app/config'

// Fonts are bundled and served from this origin: loading them from a third-party CDN would disclose
// every visitor's IP address to that provider. Variable fonts serve every weight from one file.
import '@fontsource-variable/hanken-grotesk/wght.css'
import '@fontsource-variable/jetbrains-mono/wght.css'
import '@fontsource-variable/space-grotesk/wght.css'
import '@/app/styles/main.css'

const app = createApp(App)

registerProviders(app)

app.mount('#app')
