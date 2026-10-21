import { createApp } from 'vue'

import App from '@/app/App.vue'
import { registerProviders, registerStaleBuildReload } from '@/app/config'

import '@fontsource-variable/hanken-grotesk/wght.css'
import '@fontsource-variable/jetbrains-mono/wght.css'
import '@fontsource-variable/space-grotesk/wght.css'
import '@/app/styles/main.css'

registerStaleBuildReload()

const app = createApp(App)

registerProviders(app)

app.mount('#app')
