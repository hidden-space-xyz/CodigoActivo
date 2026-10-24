import { flushPromises } from '@vue/test-utils'
import { describe, expect, it, vi } from 'vitest'

import LegalPolicyEditor from '@/pages/admin/legal-policies/ui/LegalPolicyEditor.vue'
import { formatDateTime } from '@/shared/lib/date'
import { RichTextEditor } from '@/shared/ui/rich-text-editor'

import { renderWithProviders, t } from '../../../../../support/render'
import { apiError, http, HttpResponse, server } from '../../../../../support/server'
import {
  buildLegalPolicy,
  buildLegalPolicyVersion,
  richText,
} from '../../../../../support/builders'
import {
  acceptMessageBox,
  click,
  expectNotification,
  findButton,
  isDialogOpen,
  openDialog,
} from '../../../../../support/dom'

const POLICY = buildLegalPolicy({ kind: 'Cookies', version: 2, content: richText('Vigente') })

function servePolicy() {
  const bodies: unknown[] = []
  server.use(
    http.get('/api/legal-policies/Cookies', () => HttpResponse.json(POLICY)),
    http.get('/api/legal-policies/Cookies/versions', () =>
      HttpResponse.json([
        buildLegalPolicyVersion({ version: 2, publishedAt: POLICY.publishedAt }),
        buildLegalPolicyVersion({ version: 1 }),
      ]),
    ),
    http.get('/api/legal-policies/Cookies/versions/1', () =>
      HttpResponse.json(
        buildLegalPolicy({ kind: 'Cookies', version: 1, content: richText('Antigua') }),
      ),
    ),
    http.put('/api/legal-policies/Cookies', async ({ request }) => {
      bodies.push(await request.json())
      return HttpResponse.json(POLICY)
    }),
  )
  return bodies
}

async function renderEditor(props: Record<string, unknown> = {}) {
  const rendered = await renderWithProviders(LegalPolicyEditor, {
    props: { kind: 'Cookies', label: 'Política de cookies', ...props },
    attach: true,
  })
  await vi.waitFor(() =>
    expect(rendered.wrapper.findAll('.legal-policy-editor__version')).toHaveLength(2),
  )
  return rendered
}

describe('LegalPolicyEditor', () => {
  it('shows the version in force, the hint and every version', async () => {
    servePolicy()
    const { wrapper } = await renderEditor({ hint: 'Nota' })

    expect(wrapper.get('.legal-policy-editor__status').text()).toBe(
      t('pages.admin.legalPolicies.status.published', {
        version: 2,
        date: formatDateTime(POLICY.publishedAt),
      }),
    )
    expect(wrapper.text()).toContain('Nota')
    const versions = wrapper.findAll('.legal-policy-editor__version')
    expect(versions[0]?.text()).toContain(t('pages.admin.legalPolicies.versions.inForce'))
    expect(versions[1]?.text()).not.toContain(t('pages.admin.legalPolicies.versions.inForce'))
    expect(findButton(t('pages.admin.legalPolicies.publish.button')).disabled).toBe(true)
  })

  it('publishes the edited text once confirmed', async () => {
    const bodies = servePolicy()
    const { wrapper } = await renderEditor()

    wrapper.findComponent(RichTextEditor).vm.$emit('update:modelValue', richText('Nueva'))
    await flushPromises()
    expect(wrapper.text()).toContain(t('pages.admin.legalPolicies.unpublishedChanges'))
    await click(findButton(t('pages.admin.legalPolicies.publish.button')))
    await acceptMessageBox()

    await expectNotification(t('pages.admin.legalPolicies.toasts.published'))
    expect(bodies).toEqual([{ content: richText('Nueva') }])
  })

  it('shows the problem of a policy left without text', async () => {
    servePolicy()
    const { wrapper } = await renderEditor()

    wrapper.findComponent(RichTextEditor).vm.$emit('update:modelValue', richText(''))
    await flushPromises()
    await click(findButton(t('pages.admin.legalPolicies.publish.button')))

    expect(wrapper.get('.ca-field__error').text()).toBe(
      t('pages.admin.legalPolicies.form.problems.contentRequired'),
    )
    await click(findButton(t('pages.admin.legalPolicies.discard')))
    expect(wrapper.text()).not.toContain(t('pages.admin.legalPolicies.unpublishedChanges'))
  })

  it('loads an earlier version into the editor', async () => {
    servePolicy()
    const { wrapper } = await renderEditor()
    const title = t('pages.admin.legalPolicies.versions.dialogTitle', { version: 1 })

    const [, older] = wrapper.findAll('.legal-policy-editor__version')
    await click(findButton(t('pages.admin.legalPolicies.versions.view'), older!.element))
    await vi.waitFor(() => expect(openDialog(title).textContent).toContain('Antigua'))
    await click(findButton(t('pages.admin.legalPolicies.versions.restore'), openDialog(title)))

    expect(isDialogOpen(title)).toBe(false)
    expect(wrapper.findComponent(RichTextEditor).props('modelValue')).toBe(richText('Antigua'))
  })

  it('reports a policy that could not be loaded', async () => {
    server.use(
      http.get('/api/legal-policies/Cookies', () => apiError(500)),
      http.get('/api/legal-policies/Cookies/versions', () => apiError(500)),
    )

    const { wrapper } = await renderWithProviders(LegalPolicyEditor, {
      props: { kind: 'Cookies', label: 'Política de cookies' },
    })

    await vi.waitFor(() =>
      expect(wrapper.text()).toContain(t('pages.admin.legalPolicies.loadError')),
    )
    expect(wrapper.findComponent(RichTextEditor).exists()).toBe(false)
  })
})
