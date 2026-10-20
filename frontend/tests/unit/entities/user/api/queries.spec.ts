import { describe, expect, it } from 'vitest'

import { userKeys, userList, userQueries } from '@/entities/user'

import { http, HttpResponse, paged, server } from '../../../../support/server'
import { buildUserResponse, userStatusTypes, userTypes } from '../../../../support/builders'
import { createTestQueryClient } from '../../../../support/render'

describe('userKeys', () => {
  it('nests the users under one root and keeps their catalogs apart', () => {
    expect(userKeys.list()).toEqual(['users', 'list'])
    expect(userKeys.types()).toEqual(['user-types'])
    expect(userKeys.statuses()).toEqual(['user-statuses'])
    expect(userList.queryKey).toEqual(userKeys.list())
  })
})

describe('user queries', () => {
  it('loads the types and the statuses, and pages the admin list', async () => {
    server.use(
      http.get('/api/users/types', () => HttpResponse.json(userTypes)),
      http.get('/api/users/status-types', () => HttpResponse.json(userStatusTypes)),
      http.get('/api/users', () => HttpResponse.json(paged([buildUserResponse()], 3))),
    )
    const client = createTestQueryClient()

    await expect(client.fetchQuery(userQueries.types())).resolves.toHaveLength(2)
    await expect(client.fetchQuery(userQueries.statuses())).resolves.toHaveLength(2)
    await expect(userList.fetchPage({ page: 1 })).resolves.toMatchObject({ total: 3 })
  })
})
