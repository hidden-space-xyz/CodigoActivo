import { describe, expect, it } from 'vitest'

import AdminLayout from '@/app/layouts/AdminLayout.vue'

import { renderWithProviders } from '../../../support/render'

describe('AdminLayout', () => {
  it('renders the page inside the admin shell', async () => {
    const { wrapper } = await renderWithProviders(AdminLayout, {
      user: { isAdmin: true },
      route: '/admin/events',
      slots: { default: '<section class="page-content">Dashboard body</section>' },
    })

    expect(wrapper.find('.admin .admin__main .page-content').text()).toBe('Dashboard body')
  })
})
