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
              { name: 'element-icons', test: /node_modules[\\/]@element-plus[\\/]icons-vue[\\/]/ },
              {
                name: 'element-base',
                test: /node_modules[\\/]element-plus[\\/]es[\\/](utils|constants|hooks|directives|locale|_virtual)[\\/]/,
              },
              {
                name: 'element-table',
                test: /node_modules[\\/]element-plus[\\/]es[\\/]components[\\/](table|table-v2)[\\/]/,
              },
              {
                name: 'element-components',
                test: /node_modules[\\/]element-plus[\\/]es[\\/]components[\\/][^\\/]+[\\/]/,
              },
              { name: 'element-plus', test: /node_modules[\\/]element-plus[\\/]/ },
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
