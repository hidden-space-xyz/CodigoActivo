import { h } from 'vue'
import { describe, expect, it } from 'vitest'

import CatalogSection from '@/pages/admin/catalogs/ui/CatalogSection.vue'

import { renderWithProviders } from '../../../../../support/render'
import { click, findButton } from '../../../../../support/dom'

describe('CatalogSection', () => {
  it('names the section after its heading and renders the catalog body', async () => {
    const { wrapper } = await renderWithProviders(CatalogSection, {
      props: { title: 'Categorías de eventos', newLabel: 'Nueva categoría' },
      slots: { default: () => h('p', { class: 'body' }, 'Contenido') },
    })

    const section = wrapper.find('section')
    const heading = wrapper.find('h2')
    expect(heading.text()).toBe('Categorías de eventos')
    expect(heading.attributes('id')).toBeTruthy()
    expect(section.attributes('aria-labelledby')).toBe(heading.attributes('id'))
    expect(wrapper.find('.body').text()).toBe('Contenido')
  })

  it('emits create from a plain primary button', async () => {
    const { wrapper } = await renderWithProviders(CatalogSection, {
      props: { title: 'Términos y condiciones', newLabel: 'Nuevos términos' },
      attach: true,
    })

    const button = findButton('Nuevos términos', wrapper.element)
    expect(button.classList).toContain('is-plain')
    expect(button.classList).toContain('el-button--primary')
    await click(button)

    expect(wrapper.emitted('create')).toHaveLength(1)
  })
})
