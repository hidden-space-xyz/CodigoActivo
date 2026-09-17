import { onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'

/**
 * Reads the parameters of an emailed account link (`/path#userId=…&code=…`) and then removes the
 * fragment from the address bar and the current history entry. One-time codes travel in the
 * fragment because browsers never send it to the server, so it stays out of access logs and
 * `Referer` headers. The returned getter yields `null` for missing or empty values and the first
 * value of a repeated key.
 */
export function useLinkFragment(): (name: string) => string | null {
  const route = useRoute()
  const router = useRouter()
  const params = new URLSearchParams(route.hash.slice(1))

  onMounted(() => {
    if (route.hash) void router.replace({ path: route.path, query: route.query })
  })

  return (name) => params.get(name) || null
}
