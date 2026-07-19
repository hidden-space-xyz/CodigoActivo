import { fileURLToPath, URL } from 'node:url'

import vue from '@vitejs/plugin-vue'
import { defineConfig, loadEnv } from 'vite'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const apiTarget = env.VITE_API_PROXY_TARGET || 'https://localhost:5001'

  return {
    plugins: [vue()],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    build: {
      chunkSizeWarningLimit: 700,
      rolldownOptions: {
        output: {
          codeSplitting: {
            groups: [
              { name: 'datatable', test: /node_modules[\\/]primevue[\\/]datatable[\\/]/ },
              { name: 'primetheme', test: /node_modules[\\/]@primeuix[\\/]themes[\\/]/ },
              { name: 'primevue', test: /node_modules[\\/](primevue|@primeuix)[\\/]/ },
              { name: 'editor', test: /node_modules[\\/](@tiptap|prosemirror-)/ },
              { name: 'charts', test: /node_modules[\\/](chart\.js|@kurkle)[\\/]/ },
            ],
          },
        },
      },
    },
    server: {
      port: 5173,
      proxy: {
        '/api': {
          target: apiTarget,
          changeOrigin: true,
          secure: false,
        },
        '/sitemap.xml': {
          target: apiTarget,
          changeOrigin: true,
          secure: false,
          rewrite: () => '/api/sitemap.xml',
        },
        '/robots.txt': {
          target: apiTarget,
          changeOrigin: true,
          secure: false,
          rewrite: () => '/api/robots.txt',
        },
      },
    },
  }
})
