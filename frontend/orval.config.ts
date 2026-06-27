import { defineConfig } from 'orval'

export default defineConfig({
  codigoActivo: {
    input: {
      target: './swagger.json',
    },
    output: {
      mode: 'tags-split',
      target: './src/shared/api/generated/endpoints',
      schemas: './src/shared/api/generated/models',
      client: 'vue-query',
      clean: true,
      prettier: false,
      override: {
        mutator: {
          path: './src/shared/api/http-client.ts',
          name: 'httpClient',
        },
        query: {
          useQuery: true,
          useMutation: true,
          signal: true,
        },
      },
    },
  },
})
