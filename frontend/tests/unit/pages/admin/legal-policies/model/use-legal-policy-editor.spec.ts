import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import { legalPolicyKeys } from '@/entities/legal-policy'
import { useLegalPolicyEditor } from '@/pages/admin/legal-policies/model/use-legal-policy-editor'

import { apiError, http, HttpResponse, server } from '../../../../../support/server'
import {
  buildLegalPolicy,
  buildLegalPolicyVersion,
  richText,
} from '../../../../../support/builders'
import {
  acceptMessageBox,
  cancelMessageBox,
  expectNotification,
  messageBox,
  textOf,
} from '../../../../../support/dom'
import { mountComposable, t } from '../../../../../support/render'

function servePolicy(content: () => string, version: () => number = () => 2) {
  server.use(
    http.get('/api/legal-policies/Privacy', () =>
      HttpResponse.json(buildLegalPolicy({ content: content(), version: version() })),
    ),
    http.get('/api/legal-policies/Privacy/versions', () =>
      HttpResponse.json([
        buildLegalPolicyVersion({ version: version() }),
        buildLegalPolicyVersion({ version: 1 }),
      ]),
    ),
  )
}

async function mountEditor() {
  const mounted = await mountComposable(() => useLegalPolicyEditor('Privacy'))
  await vi.waitFor(() => expect(mounted.result.policy.value).toBeTruthy())
  return mounted
}

describe('useLegalPolicyEditor', () => {
  it('starts from the version in force and discards edits', async () => {
    servePolicy(() => richText('Vigente'))
    const { result } = await mountEditor()

    expect(result.form.draft.content).toBe(richText('Vigente'))
    expect(result.dirty.value).toBe(false)
    await vi.waitFor(() => expect(result.versions.value).toHaveLength(2))

    result.form.draft.content = richText('Editada')
    expect(result.dirty.value).toBe(true)
    result.discard()

    expect(result.form.draft.content).toBe(richText('Vigente'))
    expect(result.dirty.value).toBe(false)
  })

  it('follows a newer version only while the draft is untouched', async () => {
    let content = richText('Primera')
    servePolicy(() => content)
    const { result, queryClient } = await mountEditor()

    content = richText('Segunda')
    await queryClient.invalidateQueries({ queryKey: legalPolicyKeys.all })
    await vi.waitFor(() => expect(result.form.draft.content).toBe(richText('Segunda')))

    result.form.draft.content = richText('Mía')
    content = richText('Tercera')
    await queryClient.invalidateQueries({ queryKey: legalPolicyKeys.all })
    await vi.waitFor(() => expect(result.policy.value?.content).toBe(richText('Tercera')))

    expect(result.form.draft.content).toBe(richText('Mía'))
  })

  it('refuses to publish a policy without text', async () => {
    servePolicy(() => richText('Vigente'))
    const { result } = await mountEditor()

    result.form.draft.content = richText(' ')
    result.publish()
    await flushPromises()

    expect(result.form.errors.value.content).toBe(
      t('pages.admin.legalPolicies.form.problems.contentRequired'),
    )
    expect(document.querySelector('.el-message-box')).toBeNull()
  })

  it('publishes the next version once confirmed', async () => {
    let content = richText('Vigente')
    let version = 2
    const bodies: unknown[] = []
    servePolicy(
      () => content,
      () => version,
    )
    server.use(
      http.put('/api/legal-policies/Privacy', async ({ request }) => {
        bodies.push(await request.json())
        content = richText('Nueva')
        version = 3
        return HttpResponse.json(buildLegalPolicy({ content, version }))
      }),
    )
    const { result } = await mountEditor()

    result.form.draft.content = richText('Nueva')
    result.publish()
    await flushPromises()
    expect(textOf(messageBox().querySelector('.el-message-box__message'))).toBe(
      t('pages.admin.legalPolicies.publish.message', { version: 3 }),
    )
    await cancelMessageBox()
    await flushPromises()
    expect(bodies).toEqual([])

    result.publish()
    await flushPromises()
    await acceptMessageBox()

    await expectNotification(t('pages.admin.legalPolicies.toasts.published'))
    expect(bodies).toEqual([{ content: richText('Nueva') }])
    expect(result.policy.value?.version).toBe(3)
    expect(result.dirty.value).toBe(false)
  })

  it('asks to publish the first version of a policy never published', async () => {
    server.use(
      http.get('/api/legal-policies/Privacy', () => apiError(404)),
      http.get('/api/legal-policies/Privacy/versions', () => apiError(404)),
    )
    const { result } = await mountComposable(() => useLegalPolicyEditor('Privacy'))
    await vi.waitFor(() => expect(result.policy.value).toBeNull())

    expect(result.versions.value).toEqual([])
    result.form.draft.content = richText('Primera')
    result.publish()
    await flushPromises()

    expect(textOf(messageBox().querySelector('.el-message-box__message'))).toBe(
      t('pages.admin.legalPolicies.publish.firstMessage'),
    )
    await cancelMessageBox()
  })

  it('reads an earlier version and loads it into the draft', async () => {
    servePolicy(() => richText('Vigente'))
    server.use(
      http.get('/api/legal-policies/Privacy/versions/1', () =>
        HttpResponse.json(buildLegalPolicy({ version: 1, content: richText('Antigua') })),
      ),
    )
    const { result } = await mountEditor()

    result.view(1)
    await vi.waitFor(() => expect(result.viewedPolicy.value?.version).toBe(1))
    result.restore(result.viewedPolicy.value!)

    expect(result.viewing.value).toBeNull()
    expect(result.form.draft.content).toBe(richText('Antigua'))
    expect(result.dirty.value).toBe(true)

    result.view(1)
    result.closeVersion()
    expect(result.viewing.value).toBeNull()
  })
})
