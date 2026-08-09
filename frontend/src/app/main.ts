import { createApp } from 'vue'

import App from '@/app/App.vue'
import { registerProviders } from '@/app/config'

import '@/app/styles/main.css'

const app = createApp(App)

registerProviders(app)

app.mount('#app')
