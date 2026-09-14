import { defineConfig } from 'orval'

const generatedRoot = process.env.ORVAL_GENERATED_ROOT ?? './src/shared/api/generated'

export default defineConfig({
  codigoActivo: {
    input: {
      target: './swagger.json',
    },
    output: {
      mode: 'tags-split',
      target: `${generatedRoot}/endpoints`,
      schemas: `${generatedRoot}/models`,
      client: 'vue-query',
      clean: true,
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
